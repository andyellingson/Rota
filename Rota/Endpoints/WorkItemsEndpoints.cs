using System.Globalization;
using Rota.DTOs;
using Rota.Models;
using Rota.Services;

namespace Rota.Endpoints
{
    /// <summary>
    /// Registers work-item-related minimal API endpoints.
    /// </summary>
    public static class WorkItemsEndpoints
    {
        /// <summary>
        /// Maps work item endpoints for querying and editing calendar work items.
        /// </summary>
        public static WebApplication MapWorkItemsEndpoints(this WebApplication app)
        {
            /// <summary>
            /// Tries to parse an incoming date-time string to a UTC <see cref="DateTime"/>.
            /// </summary>
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

            app.MapGet("/api/workitems", async (string? start, string? end, IWorkItemsService WorkItems, IUserService users, Microsoft.AspNetCore.Http.HttpContext context) =>
            {
                try
                {
                    var username = context.User.Identity?.Name;
                    if (string.IsNullOrEmpty(username))
                        return Results.Json(new { ok = false, message = "Unauthorized", code = 401 }, statusCode: 401);

                    var caller = await users.GetByUsernameAsync(username);
                    string? queryTeamCode = null;

                    if (context.User.IsInRole(Roles.Manager) || context.User.IsInRole(Roles.Admin))
                    {
                        // Managers query their own roster by their team code.
                        queryTeamCode = caller?.TeamCode;
                    }
                    else if (context.User.IsInRole(Roles.Employee))
                    {
                        // Employees query their linked manager's team roster.
                        if (!string.IsNullOrEmpty(caller?.TeamCode))
                        {
                            var manager = await users.GetByManagerCodeAsync(caller.TeamCode);
                            queryTeamCode = manager?.TeamCode;
                        }
                    }

                    var startDate = DateOnly.FromDateTime(DateTime.Today.AddMonths(-1));
                    var endDate = DateOnly.FromDateTime(DateTime.Today.AddMonths(1));

                    if (!string.IsNullOrWhiteSpace(start) && !DateOnly.TryParse(start, out startDate))
                        return Results.Json(new { ok = false, message = "Invalid start date", code = 400 }, statusCode: 400);

                    if (!string.IsNullOrWhiteSpace(end) && !DateOnly.TryParse(end, out endDate))
                        return Results.Json(new { ok = false, message = "Invalid end date", code = 400 }, statusCode: 400);

                    var items = await WorkItems.GetWorkItemsAsync(username, caller?.Id, queryTeamCode, startDate, endDate);
                    return Results.Json(new { ok = true, WorkItems = items, code = 200 }, statusCode: 200);
                }
                catch (Exception ex)
                {
                    app.Logger.LogError(ex, "Unhandled exception in GET /api/workitems");
                    return Results.Json(new { ok = false, message = "Internal server error", code = 500 }, statusCode: 500);
                }
            }).RequireAuthorization();

            app.MapPost("/api/workitems", async (CreateWorkItemDto dto, IWorkItemsService WorkItems, IUserService users, Microsoft.AspNetCore.Http.HttpContext context) =>
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

                    var creator = await users.GetByUsernameAsync(username);

                    var WorkItem = new WorkItem
                    {
                        Username = username,
                        Start = startUtc,
                        End = endUtc,
                        Title = dto.Title,
                        Notes = dto.Notes,
                        WorkerType = string.IsNullOrWhiteSpace(dto.WorkerType) ? "General" : dto.WorkerType,
                        Color = dto.Color,
                        AssignedToUserId = string.IsNullOrWhiteSpace(dto.AssignedToUserId) ? null : dto.AssignedToUserId,
                        TeamCode = creator?.TeamCode,
                        SeriesId = dto.SeriesId
                    };

                    var created = await WorkItems.CreateWorkItemAsync(WorkItem);
                    return Results.Json(new { ok = true, WorkItem = created, code = 200 }, statusCode: 200);
                }
                catch (Exception ex)
                {
                    app.Logger.LogError(ex, "Unhandled exception in POST /api/workitems");
                    return Results.Json(new { ok = false, message = "Internal server error", code = 500 }, statusCode: 500);
                }
            }).RequireAuthorization();

            app.MapDelete("/api/workitems/{id}", async (string id, IWorkItemsService WorkItems, Microsoft.AspNetCore.Http.HttpContext context) =>
            {
                try
                {
                    var username = context.User.Identity?.Name;
                    if (string.IsNullOrEmpty(username))
                        return Results.Json(new { ok = false, message = "Unauthorized", code = 401 }, statusCode: 401);

                    var deleted = await WorkItems.DeleteWorkItemAsync(id, username);
                    if (!deleted)
                        return Results.Json(new { ok = false, message = "Work item not found or access denied", code = 404 }, statusCode: 404);

                    return Results.Json(new { ok = true, message = "Deleted", code = 200 }, statusCode: 200);
                }
                catch (Exception ex)
                {
                    app.Logger.LogError(ex, "Unhandled exception in DELETE /api/workitems/{Id}", id);
                    return Results.Json(new { ok = false, message = "Internal server error", code = 500 }, statusCode: 500);
                }
            }).RequireAuthorization();

            app.MapPut("/api/workitems/{id}", async (string id, UpdateWorkItemDto dto, IWorkItemsService WorkItems, Microsoft.AspNetCore.Http.HttpContext context) =>
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

                    var workerTypeName = string.IsNullOrWhiteSpace(dto.WorkerType) ? "General" : dto.WorkerType;

                    var updated = await WorkItems.UpdateWorkItemAsync(id, username, startUtc, endUtc, dto.Title, dto.Notes, workerTypeName, dto.Color, string.IsNullOrWhiteSpace(dto.AssignedToUserId) ? null : dto.AssignedToUserId);
                    if (updated is null)
                        return Results.Json(new { ok = false, message = "Work item not found or access denied", code = 404 }, statusCode: 404);

                    return Results.Json(new { ok = true, WorkItem = updated, code = 200 }, statusCode: 200);
                }
                catch (Exception ex)
                {
                    app.Logger.LogError(ex, "Unhandled exception in PUT /api/workitems/{Id}", id);
                    return Results.Json(new { ok = false, message = "Internal server error", code = 500 }, statusCode: 500);
                }
            }).RequireAuthorization();

            app.MapDelete("/api/workitems/series/{seriesId}", async (Guid seriesId, IWorkItemsService WorkItems, Microsoft.AspNetCore.Http.HttpContext context) =>
            {
                try
                {
                    var username = context.User.Identity?.Name;
                    if (string.IsNullOrEmpty(username))
                        return Results.Json(new { ok = false, message = "Unauthorized", code = 401 }, statusCode: 401);

                    var deletedCount = await WorkItems.DeleteWorkItemsBySeriesIdAsync(seriesId, username);
                    return Results.Json(new { ok = true, deletedCount = deletedCount, message = $"Deleted {deletedCount} work items", code = 200 }, statusCode: 200);
                }
                catch (Exception ex)
                {
                    app.Logger.LogError(ex, "Unhandled exception in DELETE /api/workitems/series/{SeriesId}", seriesId);
                    return Results.Json(new { ok = false, message = "Internal server error", code = 500 }, statusCode: 500);
                }
            }).RequireAuthorization();

            return app;
        }
    }
}
