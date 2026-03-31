using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Logging;
using Rota.DTOs;
using Rota.Services;

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
                    var claims = new List<Claim>
                    {
                        new Claim(ClaimTypes.Name, user!.Username),
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

                    // Sign in using the cookie authentication handler
                    await httpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, new Microsoft.AspNetCore.Authentication.AuthenticationProperties { IsPersistent = true });
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
            app.MapPost("/api/auth/register", async (Rota.DTOs.RegisterDto dto, IUserService users, Microsoft.AspNetCore.Http.IHttpContextAccessor accessor) =>
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

                    var newUser = new User
                    {
                        Username = dto.Username,
                        DisplayName = string.IsNullOrWhiteSpace(dto.DisplayName) ? null : dto.DisplayName.Trim(),
                        Roles = string.IsNullOrWhiteSpace(dto.Role)
                            ? System.Array.Empty<string>()
                            : new[] { dto.Role.Trim().ToLowerInvariant() },
                        ManagerCode = isManager ? Guid.NewGuid().ToString() : null
                    };
                    await users.CreateUserAsync(newUser, dto.Password);

                    // If an employee provided a manager code, link them now
                    if (!isManager && !string.IsNullOrWhiteSpace(dto.LinkManagerCode))
                        await users.LinkToManagerAsync(newUser.Username, dto.LinkManagerCode.Trim());

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

                    await httpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, new Microsoft.AspNetCore.Authentication.AuthenticationProperties { IsPersistent = dto.RememberMe });
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

        // NOTE: DTOs are defined in the Rota.DTOs namespace (separate files).
    }
}
