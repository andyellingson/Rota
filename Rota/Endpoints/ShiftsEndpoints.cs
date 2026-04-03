using System.Globalization;
using Microsoft.Extensions.Logging;
using Rota.DTOs;
using Rota.Models;
using Rota.Services;

namespace Rota.Endpoints
{
    public static class ShiftsEndpoints
    {
        public static WebApplication MapShiftsEndpoints(this WebApplication app)
        {
            static bool TryParseDateTimeUtc(string input, out DateTime dt)
            {
                dt = default;
                if (DateTimeOffset.TryParse(input, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dto))
                {
                    dt = dto.UtcDateTime;
                    return true;
                }
                return false;
            }

            app.MapGet("/api/shifts", async (string? start, string? end, IShiftsService shifts, IUserService users, Microsoft.AspNetCore.Http.HttpContext context) =>
            {
                try
                {
                    var username = context.User.Identity?.Name;
                    if (string.IsNullOrEmpty(username))
                        return Results.Json(new { ok = false, message = "Unauthorized", code = 401 }, statusCode: 401);

                    var caller = await users.GetByUsernameAsync(username);
                    string? queryManagerCode = null;

                    if (context.User.IsInRole(Roles.Manager) || context.User.IsInRole(Roles.Admin))
                    {
                        // Managers query their own roster by their ManagerCode
                        queryManagerCode = caller?.ManagerCode;
                    }
                    else if (context.User.IsInRole(Roles.Employee))
                    {
                        // Employees query their linked manager's roster
                        if (!string.IsNullOrEmpty(caller?.ManagerUsername))
                        {
                            var manager = await users.GetByUsernameAsync(caller.ManagerUsername);
                            queryManagerCode = manager?.ManagerCode;
                        }
                    }

                    var startDate = string.IsNullOrEmpty(start) ? DateOnly.FromDateTime(DateTime.Today.AddMonths(-1)) : DateOnly.Parse(start);
                    var endDate = string.IsNullOrEmpty(end) ? DateOnly.FromDateTime(DateTime.Today.AddMonths(1)) : DateOnly.Parse(end);

                    var items = await shifts.GetShiftsAsync(username, caller?.Id, queryManagerCode, startDate, endDate);
                    return Results.Json(new { ok = true, shifts = items, code = 200 }, statusCode: 200);
                }
                catch (Exception ex)
                {
                    app.Logger.LogError(ex, "Unhandled exception in GET /api/shifts");
                    return Results.Json(new { ok = false, message = "Internal server error", code = 500 }, statusCode: 500);
                }
            }).RequireAuthorization();

            app.MapPost("/api/shifts", async (CreateShiftDto dto, IShiftsService shifts, IUserService users, Microsoft.AspNetCore.Http.HttpContext context) =>
            {
                try
                {
                    var username = context.User.Identity?.Name;
                    if (string.IsNullOrEmpty(username))
                        return Results.Json(new { ok = false, message = "Unauthorized", code = 401 }, statusCode: 401);

                    if (!TryParseDateTimeUtc(dto.Start, out var startUtc) || !TryParseDateTimeUtc(dto.End, out var endUtc))
                        return Results.Json(new { ok = false, message = "Invalid date format", code = 400 }, statusCode: 400);

                    if (endUtc <= startUtc)
                        return Results.Json(new { ok = false, message = "End must be after start", code = 400 }, statusCode: 400);

                    if (!Enum.TryParse<WorkerType>(dto.WorkerType ?? string.Empty, true, out var wt))
                        wt = WorkerType.General;

                    var creator = await users.GetByUsernameAsync(username);

                    var shift = new Shift
                    {
                        Username = username,
                        Start = startUtc,
                        End = endUtc,
                        Title = dto.Title,
                        Notes = dto.Notes,
                        WorkerType = wt,
                        Color = dto.Color,
                        AssignedToUserId = string.IsNullOrWhiteSpace(dto.AssignedToUserId) ? null : dto.AssignedToUserId,
                        ManagerCode = creator?.ManagerCode,
                        SeriesId = dto.SeriesId
                    };

                    var created = await shifts.CreateShiftAsync(shift);
                    return Results.Json(new { ok = true, shift = created, code = 200 }, statusCode: 200);
                }
                catch (Exception ex)
                {
                    app.Logger.LogError(ex, "Unhandled exception in POST /api/shifts");
                    return Results.Json(new { ok = false, message = "Internal server error", code = 500 }, statusCode: 500);
                }
            }).RequireAuthorization();

            app.MapDelete("/api/shifts/{id}", async (string id, IShiftsService shifts, Microsoft.AspNetCore.Http.HttpContext context) =>
            {
                try
                {
                    var username = context.User.Identity?.Name;
                    if (string.IsNullOrEmpty(username))
                        return Results.Json(new { ok = false, message = "Unauthorized", code = 401 }, statusCode: 401);

                    var deleted = await shifts.DeleteShiftAsync(id, username);
                    if (!deleted)
                        return Results.Json(new { ok = false, message = "Shift not found or access denied", code = 404 }, statusCode: 404);

                    return Results.Json(new { ok = true, message = "Deleted", code = 200 }, statusCode: 200);
                }
                catch (Exception ex)
                {
                    app.Logger.LogError(ex, "Unhandled exception in DELETE /api/shifts/{Id}", id);
                    return Results.Json(new { ok = false, message = "Internal server error", code = 500 }, statusCode: 500);
                }
            }).RequireAuthorization();

            app.MapPut("/api/shifts/{id}", async (string id, UpdateShiftDto dto, IShiftsService shifts, Microsoft.AspNetCore.Http.HttpContext context) =>
            {
                try
                {
                    var username = context.User.Identity?.Name;
                    if (string.IsNullOrEmpty(username))
                        return Results.Json(new { ok = false, message = "Unauthorized", code = 401 }, statusCode: 401);

                    if (!TryParseDateTimeUtc(dto.Start, out var startUtc) || !TryParseDateTimeUtc(dto.End, out var endUtc))
                        return Results.Json(new { ok = false, message = "Invalid date format", code = 400 }, statusCode: 400);

                    if (endUtc <= startUtc)
                        return Results.Json(new { ok = false, message = "End must be after start", code = 400 }, statusCode: 400);

                    if (!Enum.TryParse<WorkerType>(dto.WorkerType ?? string.Empty, true, out var wt))
                        wt = WorkerType.General;

                    var updated = await shifts.UpdateShiftAsync(id, username, startUtc, endUtc, dto.Title, dto.Notes, wt, dto.Color, string.IsNullOrWhiteSpace(dto.AssignedToUserId) ? null : dto.AssignedToUserId);
                    if (updated is null)
                        return Results.Json(new { ok = false, message = "Shift not found or access denied", code = 404 }, statusCode: 404);

                    return Results.Json(new { ok = true, shift = updated, code = 200 }, statusCode: 200);
                }
                catch (Exception ex)
                {
                    app.Logger.LogError(ex, "Unhandled exception in PUT /api/shifts/{Id}", id);
                    return Results.Json(new { ok = false, message = "Internal server error", code = 500 }, statusCode: 500);
                }
            }).RequireAuthorization();

            app.MapDelete("/api/shifts/series/{seriesId}", async (Guid seriesId, IShiftsService shifts, Microsoft.AspNetCore.Http.HttpContext context) =>
            {
                try
                {
                    var username = context.User.Identity?.Name;
                    if (string.IsNullOrEmpty(username))
                        return Results.Json(new { ok = false, message = "Unauthorized", code = 401 }, statusCode: 401);

                    var deletedCount = await shifts.DeleteShiftsBySeriesIdAsync(seriesId, username);
                    return Results.Json(new { ok = true, deletedCount = deletedCount, message = $"Deleted {deletedCount} shifts", code = 200 }, statusCode: 200);
                }
                catch (Exception ex)
                {
                    app.Logger.LogError(ex, "Unhandled exception in DELETE /api/shifts/series/{SeriesId}", seriesId);
                    return Results.Json(new { ok = false, message = "Internal server error", code = 500 }, statusCode: 500);
                }
            }).RequireAuthorization();

            return app;
        }
    }
}
