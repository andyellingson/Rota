using System.Security.Claims;
using System.Globalization;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Logging;
using Rota.DTOs;
using Rota.Models;
using Rota.Services;

namespace Rota.Endpoints
{
    /// <summary>
    /// Extension methods to register reminder-related minimal API endpoints.
    /// </summary>
    public static class RemindersEndpoints
    {
        /// <summary>
        /// Maps the reminders endpoints used by the calendar component.
        /// Registers GET /api/reminders, POST /api/reminders, and DELETE /api/reminders/{id}.
        /// </summary>
        /// <param name="app">The <see cref="WebApplication"/> to map endpoints on.</param>
        /// <returns>The <see cref="WebApplication"/> instance for chaining.</returns>
        public static WebApplication MapRemindersEndpoints(this WebApplication app)
        {
            static bool TryParseReminderDateTimeUtc(string input, out DateTime reminderDateTimeUtc)
            {
                reminderDateTimeUtc = default;

                if (DateTimeOffset.TryParse(input, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dtoDateTimeOffset))
                {
                    reminderDateTimeUtc = dtoDateTimeOffset.UtcDateTime;
                    return true;
                }

                if (DateOnly.TryParse(input, out var dateOnly))
                {
                    reminderDateTimeUtc = dateOnly.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
                    return true;
                }

                return false;
            }

            // Get reminders for the current user within a date range
            app.MapGet("/api/reminders", async (string? start, string? end, IRemindersService reminders, IUserService users, Microsoft.AspNetCore.Http.HttpContext context) =>
            {
                try
                {
                    var username = context.User.Identity?.Name;
                    if (string.IsNullOrEmpty(username))
                        return Results.Json(new { ok = false, message = "Unauthorized", code = 401 }, statusCode: 401);

                    var caller = await users.GetByUsernameAsync(username);
                    string? queryManagerCode = null;
                    var queryOwnerId = caller?.Id;

                    if (context.User.IsInRole(Roles.Manager) || context.User.IsInRole(Roles.Admin))
                    {
                        // Managers fetch their roster reminders by ManagerCode plus personal ones by OwnerId
                        queryManagerCode = caller?.ManagerCode;
                    }
                    else if (context.User.IsInRole(Roles.Employee))
                    {
                        // Employees fetch manager's reminders by ManagerCode plus their own by OwnerId
                        if (!string.IsNullOrEmpty(caller?.ManagerUsername))
                        {
                            var manager = await users.GetByUsernameAsync(caller.ManagerUsername);
                            queryManagerCode = manager?.ManagerCode;
                        }
                    }

                    var startDate = string.IsNullOrEmpty(start) ? DateOnly.FromDateTime(DateTime.Today.AddMonths(-1)) : DateOnly.Parse(start);
                    var endDate = string.IsNullOrEmpty(end) ? DateOnly.FromDateTime(DateTime.Today.AddMonths(1)) : DateOnly.Parse(end);

                    var items = await reminders.GetCalendarRemindersAsync(queryManagerCode, queryOwnerId, startDate, endDate);
                    return Results.Json(new { ok = true, reminders = items, code = 200 }, statusCode: 200);
                }
                catch (Exception ex)
                {
                    app.Logger.LogError(ex, "Unhandled exception in GET /api/reminders");
                    return Results.Json(new { ok = false, message = "Internal server error", code = 500 }, statusCode: 500);
                }
            }).RequireAuthorization();

            // Create a new reminder for the current user
            app.MapPost("/api/reminders", async (CreateReminderDto dto, IRemindersService reminders, IUserService users, Microsoft.AspNetCore.Http.HttpContext context) =>
            {
                try
                {
                    var username = context.User.Identity?.Name;
                    if (string.IsNullOrEmpty(username))
                        return Results.Json(new { ok = false, message = "Unauthorized", code = 401 }, statusCode: 401);

                    if (string.IsNullOrWhiteSpace(dto.Title))
                        return Results.Json(new { ok = false, message = "Title is required", code = 400 }, statusCode: 400);

                    // Parse the scheduled date/time from the DTO (accepts either full ISO-8601 date-time or date-only)
                    if (!TryParseReminderDateTimeUtc(dto.Date, out var reminderDateTimeUtc))
                        return Results.Json(new { ok = false, message = "Invalid date format", code = 400 }, statusCode: 400);

                    var creator = await users.GetByUsernameAsync(username);
                    var isManager = context.User.IsInRole(Roles.Manager) || context.User.IsInRole(Roles.Admin);
                    string? managerUsername = null;
                    string? managerCode = null;

                    if (isManager)
                    {
                        managerUsername = username;
                        managerCode = creator?.ManagerCode;
                    }
                    else if (!string.IsNullOrEmpty(creator?.ManagerUsername))
                    {
                        managerUsername = creator.ManagerUsername;
                        var manager = await users.GetByUsernameAsync(creator.ManagerUsername);
                        managerCode = manager?.ManagerCode;
                    }

                    var targetUsername = string.IsNullOrWhiteSpace(dto.ForUsername) ? username : dto.ForUsername;
                    User? targetUser = null;

                    if (!string.IsNullOrEmpty(managerUsername))
                    {
                        var linkedUsers = await users.GetLinkedUsersForManagerAsync(managerUsername);
                        targetUser = linkedUsers.FirstOrDefault(u => u.Username == targetUsername);
                    }

                    targetUser ??= targetUsername == username ? creator : await users.GetByUsernameAsync(targetUsername);
                    if (targetUser is null)
                        return Results.Json(new { ok = false, message = "Target user not found", code = 400 }, statusCode: 400);

                    var reminder = new Reminder
                    {
                        Username = username,
                        OwnerId = targetUser.Id,
                        ManagerCode = managerCode,
                        Date = reminderDateTimeUtc,
                        Title = dto.Title,
                        Notes = dto.Notes,
                        Color = dto.Color ?? "#ffeb3b",
                        ForUsername = targetUser.Username,
                        ForDisplayName = targetUser.DisplayName ?? targetUser.Username
                    };

                    var created = await reminders.CreateReminderAsync(reminder);
                    return Results.Json(new { ok = true, reminder = created, code = 200 }, statusCode: 200);
                }
                catch (Exception ex)
                {
                    app.Logger.LogError(ex, "Unhandled exception in POST /api/reminders");
                    return Results.Json(new { ok = false, message = "Internal server error", code = 500 }, statusCode: 500);
                }
            }).RequireAuthorization();

            // Delete a reminder by ID (only if owned by current user)
            app.MapDelete("/api/reminders/{id}", async (string id, IRemindersService reminders, Microsoft.AspNetCore.Http.HttpContext context) =>
            {
                try
                {
                    var username = context.User.Identity?.Name;
                    if (string.IsNullOrEmpty(username))
                        return Results.Json(new { ok = false, message = "Unauthorized", code = 401 }, statusCode: 401);

                    var deleted = await reminders.DeleteReminderAsync(id, username);
                    if (!deleted)
                        return Results.Json(new { ok = false, message = "Reminder not found or access denied", code = 404 }, statusCode: 404);

                    return Results.Json(new { ok = true, message = "Deleted", code = 200 }, statusCode: 200);
                }
                catch (Exception ex)
                {
                    app.Logger.LogError(ex, "Unhandled exception in DELETE /api/reminders/{Id}", id);
                    return Results.Json(new { ok = false, message = "Internal server error", code = 500 }, statusCode: 500);
                }
            }).RequireAuthorization();

            // Update an existing reminder (only if owned by current user)
            app.MapPut("/api/reminders/{id}", async (string id, Rota.DTOs.UpdateReminderDto dto, IRemindersService reminders, IUserService users, Microsoft.AspNetCore.Http.HttpContext context) =>
            {
                try
                {
                    var username = context.User.Identity?.Name;
                    if (string.IsNullOrEmpty(username))
                        return Results.Json(new { ok = false, message = "Unauthorized", code = 401 }, statusCode: 401);

                    if (string.IsNullOrWhiteSpace(dto.Title))
                        return Results.Json(new { ok = false, message = "Title is required", code = 400 }, statusCode: 400);

                    if (!TryParseReminderDateTimeUtc(dto.Date, out var reminderDateTimeUtc))
                        return Results.Json(new { ok = false, message = "Invalid date format", code = 400 }, statusCode: 400);

                    var creator = await users.GetByUsernameAsync(username);
                    var isManager = context.User.IsInRole(Roles.Manager) || context.User.IsInRole(Roles.Admin);
                    string? managerUsername = null;
                    string? managerCode = null;

                    if (isManager)
                    {
                        managerUsername = username;
                        managerCode = creator?.ManagerCode;
                    }
                    else if (!string.IsNullOrEmpty(creator?.ManagerUsername))
                    {
                        managerUsername = creator.ManagerUsername;
                        var manager = await users.GetByUsernameAsync(creator.ManagerUsername);
                        managerCode = manager?.ManagerCode;
                    }

                    var targetUsername = string.IsNullOrWhiteSpace(dto.ForUsername) ? username : dto.ForUsername;
                    User? targetUser = null;

                    if (!string.IsNullOrEmpty(managerUsername))
                    {
                        var linkedUsers = await users.GetLinkedUsersForManagerAsync(managerUsername);
                        targetUser = linkedUsers.FirstOrDefault(u => u.Username == targetUsername);
                    }

                    targetUser ??= targetUsername == username ? creator : await users.GetByUsernameAsync(targetUsername);
                    if (targetUser is null)
                        return Results.Json(new { ok = false, message = "Target user not found", code = 400 }, statusCode: 400);

                    var updated = await reminders.UpdateReminderAsync(id, username, dto.Title, dto.Notes, reminderDateTimeUtc, dto.Color ?? "#ffeb3b", targetUser.Id, targetUser.Username, targetUser.DisplayName ?? targetUser.Username, managerCode);
                    if (updated is null)
                        return Results.Json(new { ok = false, message = "Reminder not found or access denied", code = 404 }, statusCode: 404);

                    return Results.Json(new { ok = true, reminder = updated, code = 200 }, statusCode: 200);
                }
                catch (Exception ex)
                {
                    app.Logger.LogError(ex, "Unhandled exception in PUT /api/reminders/{Id}", id);
                    return Results.Json(new { ok = false, message = "Internal server error", code = 500 }, statusCode: 500);
                }
            }).RequireAuthorization();

            return app;
        }
    }
}

