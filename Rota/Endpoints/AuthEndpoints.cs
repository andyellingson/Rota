using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Rota.DTOs;
using Rota.Services;
using System.Security.Claims;

namespace Rota.Endpoints
{
    /// <summary>
    /// Extension methods to register authentication-related minimal API endpoints.
    /// </summary>
    public static class AuthEndpoints
    {
        /// <summary>
        /// Maps the auth endpoints used by the Blazor login component.
        /// Registers POST /api/auth/login and POST /api/auth/logout.
        /// </summary>
        /// <param name="app">The <see cref="WebApplication"/> to map endpoints on.</param>
        /// <returns>The <see cref="WebApplication"/> instance for chaining.</returns>
        public static WebApplication MapAuthEndpoints(this WebApplication app)
        {
            // Login endpoint: validate credentials, create claims principal and sign in using cookie auth.
            app.MapPost("/api/auth/login", async (LoginDto dto, IUserService users, Microsoft.AspNetCore.Http.IHttpContextAccessor accessor) =>
            {
                try
                {
                    // Basic DTO validation
                    if (string.IsNullOrWhiteSpace(dto.Username) || string.IsNullOrWhiteSpace(dto.Password))
                        return Results.Json(new { ok = false, message = "Username and password are required", code = 400 }, statusCode: 400);

                    // Validate credentials against user store
                    var valid = await users.ValidateCredentialsAsync(dto.Username, dto.Password);
                    if (!valid)
                        return Results.Json(new { ok = false, message = "Invalid username or password", code = 401 }, statusCode: 401);

                    // Load user and construct claims
                    var user = await users.GetByUsernameAsync(dto.Username);
                    if (user is null)
                    {
                        app.Logger.LogWarning("Validated credentials but no user record was found for {User}", dto.Username);
                        return Results.Json(new { ok = false, message = "Unable to sign in", code = 500 }, statusCode: 500);
                    }

                    var claims = new List<Claim>
                    {
                        new Claim(ClaimTypes.Name, user.Username),
                        new Claim(ClaimTypes.GivenName, user.DisplayName ?? user.Username)
                    };
                    foreach (var role in user.Roles ?? System.Array.Empty<string>())
                    {
                        claims.Add(new Claim(ClaimTypes.Role, role));
                    }

                    var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                    var principal = new ClaimsPrincipal(identity);

                    // Ensure we have a valid HttpContext to perform sign-in
                    var httpContext = accessor.HttpContext;
                    if (httpContext is null)
                    {
                        app.Logger.LogWarning("HttpContext was null during login for user {User}", dto.Username);
                        return Results.Json(new { ok = false, message = "Unable to sign in", code = 500 }, statusCode: 500);
                    }

                    // Sign in using a persistent cookie so auth survives browser navigation/restart
                    // and automatically expires after 24 hours.
                    await httpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, new Microsoft.AspNetCore.Authentication.AuthenticationProperties
                    {
                        IsPersistent = true,
                        ExpiresUtc = DateTimeOffset.UtcNow.AddHours(24)
                    });
                    return Results.Json(new { ok = true, message = "OK", code = 200 }, statusCode: 200);
                }
                catch (Exception ex)
                {
                    // Log unexpected errors and return a generic error response so internal details are not exposed to clients
                    app.Logger.LogError(ex, "Unhandled exception in /api/auth/login");
                    return Results.Json(new { ok = false, message = "Internal server error", code = 500 }, statusCode: 500);
                }
            });

            // Register endpoint: create a new user and sign them in.
            app.MapPost("/api/auth/register", async (Rota.DTOs.RegisterDto dto, IUserService users, ISchedulesService schedules, Microsoft.AspNetCore.Http.IHttpContextAccessor accessor) =>
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(dto.Username) || string.IsNullOrWhiteSpace(dto.Password))
                        return Results.Json(new { ok = false, message = "Username and password are required", code = 400 }, statusCode: 400);

                    // Ensure username is not already in use
                    var existing = await users.GetByUsernameAsync(dto.Username);
                    if (existing is not null)
                        return Results.Json(new { ok = false, message = "User already exists", code = 409 }, statusCode: 409);

                    var isManager = dto.Role?.Trim().ToLowerInvariant() == "manager";

                    // Auto-derive display name from first/last name when not explicitly provided
                    var derivedDisplayName = string.IsNullOrWhiteSpace(dto.DisplayName)
                        ? BuildDisplayName(dto.FirstName, dto.LastName)
                        : dto.DisplayName.Trim();

                    var newUser = new Rota.Models.User
                    {
                        Username = dto.Username,
                        FirstName = string.IsNullOrWhiteSpace(dto.FirstName) ? null : dto.FirstName.Trim(),
                        LastName = string.IsNullOrWhiteSpace(dto.LastName) ? null : dto.LastName.Trim(),
                        DisplayName = string.IsNullOrWhiteSpace(derivedDisplayName) ? null : derivedDisplayName,
                        Roles = string.IsNullOrWhiteSpace(dto.Role)
                            ? System.Array.Empty<string>()
                            : new[] { dto.Role.Trim().ToLowerInvariant() },
                        ManagerCode = isManager ? Guid.NewGuid().ToString() : null,
                        // Employee users default to 40 max hours per week.
                        MaxHours = isManager ? 0 : 40,
                        // New users default to all-day availability for every day of the week.
                        Availability = BuildDefaultAvailability()
                    };
                    await users.CreateUserAsync(newUser, dto.Password);

                    // If an employee provided a manager code, link them now and default schedule
                    // assignments to every schedule currently owned by that manager.
                    if (!isManager && !string.IsNullOrWhiteSpace(dto.LinkManagerCode))
                    {
                        var managerCode = dto.LinkManagerCode.Trim();
                        var linked = await users.LinkToManagerAsync(newUser.Username, managerCode);
                        if (linked)
                        {
                            var manager = await users.GetByManagerCodeAsync(managerCode);
                            if (!string.IsNullOrWhiteSpace(manager?.Id))
                            {
                                var managerSchedules = await schedules.GetSchedulesForManagerAsync(manager.Id, manager.ManagerCode);
                                var managerScheduleIds = managerSchedules
                                    .Select(s => s.Id)
                                    .Where(id => !string.IsNullOrWhiteSpace(id))
                                    .Select(id => id!)
                                    .ToList();
                                await users.UpdateAssignedSchedulesAsync(newUser.Username, managerScheduleIds);
                            }
                        }
                    }

                    // Reload so ManagerUsername is reflected in claims if linking succeeded
                    newUser = await users.GetByUsernameAsync(newUser.Username) ?? newUser;

                    // Build claims from the newly created user and sign in
                    var claims = new List<Claim>
                    {
                        new Claim(ClaimTypes.Name, newUser.Username),
                        new Claim(ClaimTypes.GivenName, newUser.DisplayName ?? newUser.Username)
                    };
                    foreach (var role in newUser.Roles ?? System.Array.Empty<string>())
                    {
                        claims.Add(new Claim(ClaimTypes.Role, role));
                    }

                    var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                    var principal = new ClaimsPrincipal(identity);

                    var httpContext = accessor.HttpContext;
                    if (httpContext is null)
                    {
                        app.Logger.LogWarning("HttpContext was null during register for user {User}", dto.Username);
                        return Results.Json(new { ok = false, message = "Unable to sign in", code = 500 }, statusCode: 500);
                    }

                    // Registration also signs in with the same persistent 24-hour auth cookie policy.
                    await httpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, new Microsoft.AspNetCore.Authentication.AuthenticationProperties
                    {
                        IsPersistent = true,
                        ExpiresUtc = DateTimeOffset.UtcNow.AddHours(24)
                    });
                    return Results.Json(new { ok = true, message = "OK", code = 200 }, statusCode: 200);
                }
                catch (Exception ex)
                {
                    app.Logger.LogError(ex, "Unhandled exception in /api/auth/register");
                    return Results.Json(new { ok = false, message = "Internal server error", code = 500 }, statusCode: 500);
                }
            });

            // Logout endpoint: sign the current user out of the cookie authentication scheme
            app.MapPost("/api/auth/logout", async (Microsoft.AspNetCore.Http.IHttpContextAccessor accessor) =>
            {
                try
                {
                    var httpContext = accessor.HttpContext;
                    if (httpContext is null)
                    {
                        app.Logger.LogWarning("HttpContext was null during logout");
                        return Results.Json(new { ok = false, message = "Unable to sign out", code = 500 }, statusCode: 500);
                    }

                    await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                    return Results.Json(new { ok = true, message = "OK", code = 200 }, statusCode: 200);
                }
                catch (Exception ex)
                {
                    app.Logger.LogError(ex, "Unhandled exception in /api/auth/logout");
                    return Results.Json(new { ok = false, message = "Internal server error", code = 500 }, statusCode: 500);
                }
            });

            return app;
        }

        /// <summary>
        /// Builds a default all-day availability window for each day of the week.
        /// </summary>
        private static List<Rota.Models.UserAvailability> BuildDefaultAvailability()
        {
            return Enum.GetValues<DayOfWeek>()
                .Select(day => new Rota.Models.UserAvailability
                {
                    DayOfWeek = day,
                    StartTime = "00:00",
                    EndTime = "23:59"
                })
                .ToList();
        }

        /// <summary>
        /// Builds a display name from first and last name parts, returning null when both are empty.
        /// </summary>
        private static string? BuildDisplayName(string? firstName, string? lastName)
        {
            var parts = new[] { firstName?.Trim(), lastName?.Trim() }
                .Where(p => !string.IsNullOrEmpty(p));
            var result = string.Join(" ", parts);
            return string.IsNullOrEmpty(result) ? null : result;
        }

        // NOTE: DTOs are defined in the Rota.DTOs namespace (separate files).
    }
}
