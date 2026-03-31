using System.Globalization;
using Rota.DTOs;
using Rota.Models;
using Rota.Services;

namespace Rota.Endpoints
{
    public static class AbsencesEndpoints
    {
        public static WebApplication MapAbsencesEndpoints(this WebApplication app)
        {
            static bool TryParseDateUtc(string input, out DateTime dt)
            {
                dt = default;
                if (DateTimeOffset.TryParse(input, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dto))
                {
                    dt = dto.UtcDateTime;
                    return true;
                }
                return false;
            }

            // GET /api/absences
            app.MapGet("/api/absences", async (string? start, string? end, IAbsencesService absences, IUserService users, Microsoft.AspNetCore.Http.HttpContext context) =>
            {
                try
                {
                    var username = context.User.Identity?.Name;
                    if (string.IsNullOrEmpty(username))
                        return Results.Json(new { ok = false, message = "Unauthorized", code = 401 }, statusCode: 401);

                    var caller = await users.GetByUsernameAsync(username);
                    string? queryManagerCode = null;
                    var queryUserId = caller?.Id;

                    if (context.User.IsInRole(Roles.Manager) || context.User.IsInRole(Roles.Admin))
                    {
                        queryManagerCode = caller?.ManagerCode;
                    }
                    else if (context.User.IsInRole(Roles.Employee))
                    {
                        if (!string.IsNullOrEmpty(caller?.ManagerUsername))
                        {
                            var manager = await users.GetByUsernameAsync(caller.ManagerUsername);
                            queryManagerCode = manager?.ManagerCode;
                        }
                    }

                    var startDate = string.IsNullOrEmpty(start) ? DateOnly.FromDateTime(DateTime.Today.AddMonths(-1)) : DateOnly.Parse(start);
                    var endDate = string.IsNullOrEmpty(end) ? DateOnly.FromDateTime(DateTime.Today.AddMonths(1)) : DateOnly.Parse(end);

                    var items = await absences.GetAbsencesAsync(queryManagerCode, queryUserId, startDate, endDate);
                    return Results.Json(new { ok = true, absences = items, code = 200 }, statusCode: 200);
                }
                catch (Exception ex)
                {
                    app.Logger.LogError(ex, "Unhandled exception in GET /api/absences");
                    return Results.Json(new { ok = false, message = "Internal server error", code = 500 }, statusCode: 500);
                }
            }).RequireAuthorization();

            // POST /api/absences
            app.MapPost("/api/absences", async (CreateAbsenceDto dto, IAbsencesService absences, IUserService users, Microsoft.AspNetCore.Http.HttpContext context) =>
            {
                try
                {
                    var username = context.User.Identity?.Name;
                    if (string.IsNullOrEmpty(username))
                        return Results.Json(new { ok = false, message = "Unauthorized", code = 401 }, statusCode: 401);

                    if (string.IsNullOrWhiteSpace(dto.Title))
                        return Results.Json(new { ok = false, message = "Title is required", code = 400 }, statusCode: 400);

                    if (!TryParseDateUtc(dto.StartDate, out var startUtc))
                        return Results.Json(new { ok = false, message = "Invalid start date", code = 400 }, statusCode: 400);

                    var dayCount = Math.Max(1, Math.Min(dto.DayCount, 365));
                    var endUtc = startUtc.AddDays(dayCount); // exclusive end (midnight after last day)

                    var creator = await users.GetByUsernameAsync(username);
                    var isManager = context.User.IsInRole(Roles.Manager) || context.User.IsInRole(Roles.Admin);

                    string? managerCode = null;
                    if (isManager)
                    {
                        managerCode = creator?.ManagerCode;
                    }
                    else if (!string.IsNullOrEmpty(creator?.ManagerUsername))
                    {
                        var mgr = await users.GetByUsernameAsync(creator.ManagerUsername);
                        managerCode = mgr?.ManagerCode;
                    }

                    var targetUsername = string.IsNullOrWhiteSpace(dto.ForUsername) ? username : dto.ForUsername;
                    User? targetUser = null;

                    if (!string.IsNullOrEmpty(managerCode))
                    {
                        var linkedUsers = await users.GetLinkedUsersForManagerAsync(isManager ? username : creator!.ManagerUsername!);
                        targetUser = linkedUsers.FirstOrDefault(u => u.Username == targetUsername);
                    }

                    targetUser ??= targetUsername == username ? creator : await users.GetByUsernameAsync(targetUsername);
                    if (targetUser is null)
                        return Results.Json(new { ok = false, message = "Target user not found", code = 400 }, statusCode: 400);

                    var absence = new Absence
                    {
                        Username = username,
                        UserId = targetUser.Id,
                        ManagerCode = managerCode,
                        StartDate = DateTime.SpecifyKind(startUtc, DateTimeKind.Utc),
                        EndDate = DateTime.SpecifyKind(endUtc, DateTimeKind.Utc),
                        DayCount = dayCount,
                        Title = dto.Title,
                        Notes = dto.Notes,
                        Color = dto.Color ?? "#fa8c16",
                        ForUsername = targetUser.Username,
                        ForDisplayName = targetUser.DisplayName ?? targetUser.Username
                    };

                    var created = await absences.CreateAbsenceAsync(absence);
                    return Results.Json(new { ok = true, absence = created, code = 200 }, statusCode: 200);
                }
                catch (Exception ex)
                {
                    app.Logger.LogError(ex, "Unhandled exception in POST /api/absences");
                    return Results.Json(new { ok = false, message = "Internal server error", code = 500 }, statusCode: 500);
                }
            }).RequireAuthorization();

            // DELETE /api/absences/{id}
            app.MapDelete("/api/absences/{id}", async (string id, IAbsencesService absences, Microsoft.AspNetCore.Http.HttpContext context) =>
            {
                try
                {
                    var username = context.User.Identity?.Name;
                    if (string.IsNullOrEmpty(username))
                        return Results.Json(new { ok = false, message = "Unauthorized", code = 401 }, statusCode: 401);

                    var deleted = await absences.DeleteAbsenceAsync(id, username);
                    if (!deleted)
                        return Results.Json(new { ok = false, message = "Not found or access denied", code = 404 }, statusCode: 404);

                    return Results.Json(new { ok = true, message = "Deleted", code = 200 }, statusCode: 200);
                }
                catch (Exception ex)
                {
                    app.Logger.LogError(ex, "Unhandled exception in DELETE /api/absences/{Id}", id);
                    return Results.Json(new { ok = false, message = "Internal server error", code = 500 }, statusCode: 500);
                }
            }).RequireAuthorization();

            // PUT /api/absences/{id}
            app.MapPut("/api/absences/{id}", async (string id, UpdateAbsenceDto dto, IAbsencesService absences, IUserService users, Microsoft.AspNetCore.Http.HttpContext context) =>
            {
                try
                {
                    var username = context.User.Identity?.Name;
                    if (string.IsNullOrEmpty(username))
                        return Results.Json(new { ok = false, message = "Unauthorized", code = 401 }, statusCode: 401);

                    if (!TryParseDateUtc(dto.StartDate, out var startUtc))
                        return Results.Json(new { ok = false, message = "Invalid start date", code = 400 }, statusCode: 400);

                    var dayCount = Math.Max(1, Math.Min(dto.DayCount, 365));
                    var endUtc = startUtc.AddDays(dayCount);

                    var creator = await users.GetByUsernameAsync(username);
                    var isManager = context.User.IsInRole(Roles.Manager) || context.User.IsInRole(Roles.Admin);

                    string? managerCode = null;
                    if (isManager)
                    {
                        managerCode = creator?.ManagerCode;
                    }
                    else if (!string.IsNullOrEmpty(creator?.ManagerUsername))
                    {
                        var mgr = await users.GetByUsernameAsync(creator.ManagerUsername);
                        managerCode = mgr?.ManagerCode;
                    }

                    var targetUsername = string.IsNullOrWhiteSpace(dto.ForUsername) ? username : dto.ForUsername;
                    User? targetUser = null;

                    if (!string.IsNullOrEmpty(managerCode))
                    {
                        var linkedUsers = await users.GetLinkedUsersForManagerAsync(isManager ? username : creator!.ManagerUsername!);
                        targetUser = linkedUsers.FirstOrDefault(u => u.Username == targetUsername);
                    }

                    targetUser ??= targetUsername == username ? creator : await users.GetByUsernameAsync(targetUsername);
                    if (targetUser is null)
                        return Results.Json(new { ok = false, message = "Target user not found", code = 400 }, statusCode: 400);

                    var updated = await absences.UpdateAbsenceAsync(id, username, dto.Title, dto.Notes,
                        DateTime.SpecifyKind(startUtc, DateTimeKind.Utc),
                        DateTime.SpecifyKind(endUtc, DateTimeKind.Utc),
                        dayCount, dto.Color, targetUser.Id, targetUser.Username, targetUser.DisplayName ?? targetUser.Username, managerCode);

                    if (updated is null)
                        return Results.Json(new { ok = false, message = "Not found or access denied", code = 404 }, statusCode: 404);

                    return Results.Json(new { ok = true, absence = updated, code = 200 }, statusCode: 200);
                }
                catch (Exception ex)
                {
                    app.Logger.LogError(ex, "Unhandled exception in PUT /api/absences/{Id}", id);
                    return Results.Json(new { ok = false, message = "Internal server error", code = 500 }, statusCode: 500);
                }
            }).RequireAuthorization();

            return app;
        }
    }
}
