using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Rota.Models;

namespace Rota.Services
{
    /// <summary>
    /// MongoDB-backed implementation of <see cref="IWorkItemsService"/>.
    /// </summary>
    public class MongoWorkItemsService : IWorkItemsService
    {
        private readonly IMongoCollection<WorkItem> _WorkItems;
        private readonly IUserService? _userService;
        private readonly ILogger<MongoWorkItemsService> _logger;

        /// <summary>
        /// Creates the service using configured MongoDB options.
        /// </summary>
        public MongoWorkItemsService(IOptions<MongoDbOptions> options, ILogger<MongoWorkItemsService> logger, IUserService? userService = null)
        {
            _logger = logger;
            _userService = userService;
            var opts = options?.Value ?? throw new ArgumentNullException(nameof(options));
            if (string.IsNullOrWhiteSpace(opts.ConnectionString))
            {
                throw new InvalidOperationException("MongoDb:ConnectionString is required.");
            }

            var client = new MongoClient(opts.ConnectionString);
            var db = client.GetDatabase(opts.DatabaseName);
            _WorkItems = db.GetCollection<WorkItem>(opts.WorkItemsCollectionName);
        }

        /// <summary>
        /// Creates the service from an injected collection instance (test-friendly).
        /// </summary>
        public MongoWorkItemsService(IMongoCollection<WorkItem> WorkItemsCollection, ILogger<MongoWorkItemsService> logger, IUserService? userService = null)
        {
            _WorkItems = WorkItemsCollection;
            _logger = logger;
            _userService = userService;
        }

        /// <inheritdoc />
        public async System.Threading.Tasks.Task<List<WorkItem>> GetWorkItemsAsync(string? username, string? userId, string? teamCode, DateOnly startDate, DateOnly endDate)
        {
            try
            {
                var start = startDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
                var end = endDate.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);
                FilterDefinition<WorkItem> userFilter;
                if (!string.IsNullOrEmpty(teamCode))
                {
                    // Primary: query by TeamCode to return the full team roster
                    userFilter = Builders<WorkItem>.Filter.Eq(s => s.TeamCode, teamCode);
                }
                else if (!string.IsNullOrEmpty(userId) || !string.IsNullOrEmpty(username))
                {
                    // Fallback: return work items either created by the username or assigned to the userId
                    var filters = new List<FilterDefinition<WorkItem>>();
                    if (!string.IsNullOrEmpty(username))
                        filters.Add(Builders<WorkItem>.Filter.Eq(s => s.Username, username));
                    if (!string.IsNullOrEmpty(userId))
                        filters.Add(Builders<WorkItem>.Filter.Eq(s => s.AssignedToUserId, userId));

                    userFilter = filters.Count == 1 ? filters[0] : Builders<WorkItem>.Filter.Or(filters);
                }
                else
                {
                    return new List<WorkItem>();
                }

                var filter = Builders<WorkItem>.Filter.And(
                    userFilter,
                    Builders<WorkItem>.Filter.Gte(s => s.Start, start),
                    Builders<WorkItem>.Filter.Lte(s => s.End, end)
                );

                return await _WorkItems.Find(filter).ToListAsync();
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error retrieving WorkItems between {Start} and {End}", startDate, endDate);
                return new List<WorkItem>();
            }
        }

        /// <inheritdoc />
        public async System.Threading.Tasks.Task<long> DeleteWorkItemsByScheduleIdAsync(string scheduleId)
        {
            try
            {
                var filter = Builders<WorkItem>.Filter.Eq(s => s.ScheduleId, scheduleId);
                var result = await _WorkItems.DeleteManyAsync(filter);
                return result.DeletedCount;
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error deleting WorkItems for schedule {ScheduleId}", scheduleId);
                throw;
            }
        }

        /// <inheritdoc />
        public async System.Threading.Tasks.Task<WorkItem> CreateWorkItemAsync(WorkItem WorkItem)
        {
            try
            {
                WorkItem.CreatedAt = DateTime.UtcNow;
                await _WorkItems.InsertOneAsync(WorkItem);
                return WorkItem;
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error creating WorkItem for user {User}", WorkItem?.Username);
                throw;
            }
        }

        /// <inheritdoc />
        public async System.Threading.Tasks.Task<bool> DeleteWorkItemAsync(string id, string username)
        {
            try
            {
                var filter = Builders<WorkItem>.Filter.And(
                    Builders<WorkItem>.Filter.Eq(s => s.Id, id),
                    Builders<WorkItem>.Filter.Eq(s => s.Username, username)
                );

                var result = await _WorkItems.DeleteOneAsync(filter);
                return result.DeletedCount > 0;
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error deleting WorkItem {Id} for user {User}", id, username);
                return false;
            }
        }

        /// <inheritdoc />
        public async System.Threading.Tasks.Task<int> DeleteWorkItemsBySeriesIdAsync(Guid seriesId, string username)
        {
            try
            {
                var filter = Builders<WorkItem>.Filter.And(
                    Builders<WorkItem>.Filter.Eq(s => s.SeriesId, seriesId),
                    Builders<WorkItem>.Filter.Eq(s => s.Username, username)
                );

                var result = await _WorkItems.DeleteManyAsync(filter);
                return (int)result.DeletedCount;
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error deleting WorkItems with seriesId {SeriesId} for user {User}", seriesId, username);
                return 0;
            }
        }

        /// <inheritdoc />
        public async System.Threading.Tasks.Task<List<string>> GetDistinctScheduleIdsForUserAsync(string userId)
        {
            try
            {
                var filter = Builders<WorkItem>.Filter.And(
                    Builders<WorkItem>.Filter.Eq(s => s.AssignedToUserId, userId),
                    Builders<WorkItem>.Filter.Ne(s => s.ScheduleId, null)
                );
                var ids = await _WorkItems.Distinct(s => s.ScheduleId, filter).ToListAsync();
                return ids.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToList();
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error getting distinct schedule IDs for user {UserId}", userId);
                return new List<string>();
            }
        }

        /// <inheritdoc />
        public async System.Threading.Tasks.Task<WorkItem?> UpdateWorkItemAsync(string id, string username, DateTime startUtc, DateTime endUtc, string? title, string? notes, string workerType, string? color, string? assignedToUserId, bool autoAssignedFromAdditionalQualifiedType = false)
        {
            try
            {
                var filter = Builders<WorkItem>.Filter.And(
                    Builders<WorkItem>.Filter.Eq(s => s.Id, id),
                    Builders<WorkItem>.Filter.Eq(s => s.Username, username)
                );

                var update = Builders<WorkItem>.Update
                    .Set(s => s.Start, DateTime.SpecifyKind(startUtc, DateTimeKind.Utc))
                    .Set(s => s.End, DateTime.SpecifyKind(endUtc, DateTimeKind.Utc))
                    .Set(s => s.Title, title)
                    .Set(s => s.Notes, notes)
                    .Set(s => s.WorkerType, workerType)
                    .Set(s => s.Color, color)
                    .Set(s => s.AssignedToUserId, assignedToUserId)
                    .Set(s => s.AutoAssignedFromAdditionalQualifiedType, autoAssignedFromAdditionalQualifiedType);

                var options = new FindOneAndUpdateOptions<WorkItem> { ReturnDocument = ReturnDocument.After };
                var updated = await _WorkItems.FindOneAndUpdateAsync(filter, update, options);
                return updated;
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error updating WorkItem {Id} for user {User}", id, username);
                return null;
            }
        }

        /// <inheritdoc />
        public async System.Threading.Tasks.Task<WorkItemCoverActionResult> RequestCoverAsync(string workItemId, string requesterUserId, string? teamCode)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(workItemId) || string.IsNullOrWhiteSpace(requesterUserId))
                    return new WorkItemCoverActionResult(false, "Invalid work item or user.", null);

                var workItem = await _WorkItems.Find(s => s.Id == workItemId).FirstOrDefaultAsync();
                if (workItem is null)
                    return new WorkItemCoverActionResult(false, "Work item was not found.", null);

                if (!string.IsNullOrWhiteSpace(teamCode) && !string.Equals(workItem.TeamCode, teamCode, StringComparison.OrdinalIgnoreCase))
                    return new WorkItemCoverActionResult(false, "You do not have access to this work item.", null);

                if (workItem.Start.ToUniversalTime() <= DateTime.UtcNow)
                    return new WorkItemCoverActionResult(false, "Cover can only be requested before the work item starts.", null);

                if (!string.Equals(workItem.AssignedToUserId, requesterUserId, StringComparison.OrdinalIgnoreCase))
                    return new WorkItemCoverActionResult(false, "Only the currently assigned team member can request cover.", null);

                var update = Builders<WorkItem>.Update
                    .Set(s => s.CoverRequested, true)
                    .Set(s => s.CoverRequestedByUserId, requesterUserId)
                    .Set(s => s.CoverRequestedAt, DateTime.UtcNow);

                var updated = await _WorkItems.FindOneAndUpdateAsync(
                    Builders<WorkItem>.Filter.Eq(s => s.Id, workItemId),
                    update,
                    new FindOneAndUpdateOptions<WorkItem> { ReturnDocument = ReturnDocument.After });

                return updated is null
                    ? new WorkItemCoverActionResult(false, "Unable to request cover for this work item.", null)
                    : new WorkItemCoverActionResult(true, null, updated);
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error requesting cover for WorkItem {Id}", workItemId);
                return new WorkItemCoverActionResult(false, "An error occurred while requesting cover.", null);
            }
        }

        /// <inheritdoc />
        public async System.Threading.Tasks.Task<WorkItemCoverActionResult> PickupCoverAsync(string workItemId, string pickerUserId, string? teamCode)
        {
            try
            {
                if (_userService is null)
                    return new WorkItemCoverActionResult(false, "User service is unavailable.", null);

                if (string.IsNullOrWhiteSpace(workItemId) || string.IsNullOrWhiteSpace(pickerUserId))
                    return new WorkItemCoverActionResult(false, "Invalid work item or user.", null);

                var workItem = await _WorkItems.Find(s => s.Id == workItemId).FirstOrDefaultAsync();
                if (workItem is null)
                    return new WorkItemCoverActionResult(false, "Work item was not found.", null);

                var scopeTeamCode = !string.IsNullOrWhiteSpace(teamCode) ? teamCode : workItem.TeamCode;
                if (!string.IsNullOrWhiteSpace(scopeTeamCode) && !string.Equals(workItem.TeamCode, scopeTeamCode, StringComparison.OrdinalIgnoreCase))
                    return new WorkItemCoverActionResult(false, "You do not have access to this work item.", null);

                if (workItem.Start.ToUniversalTime() <= DateTime.UtcNow)
                    return new WorkItemCoverActionResult(false, "Cover can only be picked up before the work item starts.", null);

                if (!workItem.CoverRequested)
                    return new WorkItemCoverActionResult(false, "This work item is not currently requesting cover.", null);

                if (string.IsNullOrWhiteSpace(workItem.AssignedToUserId))
                    return new WorkItemCoverActionResult(false, "This work item is currently unassigned.", null);

                if (string.Equals(workItem.AssignedToUserId, pickerUserId, StringComparison.OrdinalIgnoreCase))
                    return new WorkItemCoverActionResult(false, "You are already assigned to this work item.", null);

                if (string.IsNullOrWhiteSpace(scopeTeamCode))
                    return new WorkItemCoverActionResult(false, "This work item is missing a team code.", null);

                var linkedUsers = await _userService.GetLinkedUsersForManagerAsync(scopeTeamCode);
                var picker = linkedUsers.FirstOrDefault(u => string.Equals(u.Id, pickerUserId, StringComparison.OrdinalIgnoreCase));
                if (picker is null)
                    return new WorkItemCoverActionResult(false, "Unable to verify your team member qualifications.", null);

                var requiredType = NormalizeWorkerType(workItem.WorkerType);
                var pickerPrimaryType = NormalizeWorkerType(picker.TeamMemberType);
                var pickerAdditionalTypes = (picker.AvailableTeamMemberTypes ?? new List<string>())
                    .Select(NormalizeWorkerType)
                    .Where(t => !string.IsNullOrWhiteSpace(t))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                var isEligible = string.Equals(requiredType, pickerPrimaryType, StringComparison.OrdinalIgnoreCase)
                    || pickerAdditionalTypes.Contains(requiredType);

                if (!isEligible)
                    return new WorkItemCoverActionResult(false, "You are not qualified to cover this work item.", null);

                // Team members may only perform one work item at a time.
                var pickerOverlapFilter = Builders<WorkItem>.Filter.And(
                    Builders<WorkItem>.Filter.Eq(s => s.AssignedToUserId, pickerUserId),
                    Builders<WorkItem>.Filter.Ne(s => s.Id, workItemId),
                    Builders<WorkItem>.Filter.Lt(s => s.Start, workItem.End),
                    Builders<WorkItem>.Filter.Gt(s => s.End, workItem.Start)
                );

                var hasOverlap = await _WorkItems.Find(pickerOverlapFilter).AnyAsync();
                if (hasOverlap)
                    return new WorkItemCoverActionResult(false, "You already have a work item during this time range and cannot pick up overlapping work.", null);

                // Enforce weekly max-hours cap when the picker has a configured limit (> 0 means capped).
                if (picker.MaxHours > 0)
                {
                    var (weekStartUtc, weekEndUtc) = GetWeekUtcBounds(workItem.Start.ToUniversalTime());

                    var pickerWeekFilter = Builders<WorkItem>.Filter.And(
                        Builders<WorkItem>.Filter.Eq(s => s.AssignedToUserId, pickerUserId),
                        Builders<WorkItem>.Filter.Ne(s => s.Id, workItemId),
                        Builders<WorkItem>.Filter.Lt(s => s.Start, weekEndUtc),
                        Builders<WorkItem>.Filter.Gt(s => s.End, weekStartUtc)
                    );

                    var weekAssignments = await _WorkItems.Find(pickerWeekFilter).ToListAsync();
                    var alreadyAssignedHours = weekAssignments.Sum(assignment => GetOverlapHours(
                        assignment.Start.ToUniversalTime(),
                        assignment.End.ToUniversalTime(),
                        weekStartUtc,
                        weekEndUtc));

                    var pickupHours = GetOverlapHours(
                        workItem.Start.ToUniversalTime(),
                        workItem.End.ToUniversalTime(),
                        weekStartUtc,
                        weekEndUtc);

                    if (alreadyAssignedHours + pickupHours > picker.MaxHours)
                        return new WorkItemCoverActionResult(false, "Picking up this work item would exceed your weekly max hours.", null);
                }

                var update = Builders<WorkItem>.Update
                    .Set(s => s.AssignedToUserId, pickerUserId)
                    .Set(s => s.CoverRequested, false)
                    .Set(s => s.CoverRequestedByUserId, null)
                    .Set(s => s.CoverRequestedAt, null)
                    .Set(s => s.AutoAssignedFromAdditionalQualifiedType, false);

                var updated = await _WorkItems.FindOneAndUpdateAsync(
                    Builders<WorkItem>.Filter.Eq(s => s.Id, workItemId),
                    update,
                    new FindOneAndUpdateOptions<WorkItem> { ReturnDocument = ReturnDocument.After });

                return updated is null
                    ? new WorkItemCoverActionResult(false, "Unable to pick up this work item.", null)
                    : new WorkItemCoverActionResult(true, null, updated);
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error picking up cover for WorkItem {Id}", workItemId);
                return new WorkItemCoverActionResult(false, "An error occurred while picking up this work item.", null);
            }
        }

        /// <inheritdoc />
        public async System.Threading.Tasks.Task<WorkItemCoverActionResult> CancelCoverAsync(string workItemId, string requesterUserId, string? teamCode)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(workItemId) || string.IsNullOrWhiteSpace(requesterUserId))
                    return new WorkItemCoverActionResult(false, "Invalid work item or user.", null);

                var workItem = await _WorkItems.Find(s => s.Id == workItemId).FirstOrDefaultAsync();
                if (workItem is null)
                    return new WorkItemCoverActionResult(false, "Work item was not found.", null);

                if (!string.IsNullOrWhiteSpace(teamCode) && !string.Equals(workItem.TeamCode, teamCode, StringComparison.OrdinalIgnoreCase))
                    return new WorkItemCoverActionResult(false, "You do not have access to this work item.", null);

                if (workItem.Start.ToUniversalTime() <= DateTime.UtcNow)
                    return new WorkItemCoverActionResult(false, "Cover can only be canceled before the work item starts.", null);

                if (!workItem.CoverRequested)
                    return new WorkItemCoverActionResult(false, "This work item is not currently requesting cover.", null);

                if (!string.Equals(workItem.AssignedToUserId, requesterUserId, StringComparison.OrdinalIgnoreCase))
                    return new WorkItemCoverActionResult(false, "Only the currently assigned team member can cancel this cover request.", null);

                var update = Builders<WorkItem>.Update
                    .Set(s => s.CoverRequested, false)
                    .Set(s => s.CoverRequestedByUserId, null)
                    .Set(s => s.CoverRequestedAt, null);

                var updated = await _WorkItems.FindOneAndUpdateAsync(
                    Builders<WorkItem>.Filter.Eq(s => s.Id, workItemId),
                    update,
                    new FindOneAndUpdateOptions<WorkItem> { ReturnDocument = ReturnDocument.After });

                return updated is null
                    ? new WorkItemCoverActionResult(false, "Unable to cancel cover for this work item.", null)
                    : new WorkItemCoverActionResult(true, null, updated);
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error canceling cover for WorkItem {Id}", workItemId);
                return new WorkItemCoverActionResult(false, "An error occurred while canceling cover.", null);
            }
        }

        /// <inheritdoc />
        public async System.Threading.Tasks.Task<List<WorkItem>> GetWorkItemTemplateWorkItemsAsync(string workItemTemplateId, string teamCode)
        {
            try
            {
                var filter = Builders<WorkItem>.Filter.And(
                    Builders<WorkItem>.Filter.Eq(s => s.RotationId, workItemTemplateId),
                    Builders<WorkItem>.Filter.Eq(s => s.TeamCode, teamCode)
                );

                var templateWorkItems = await _WorkItems.Find(filter).ToListAsync();
                return templateWorkItems;
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error getting template WorkItems for Work Item Template {WorkItemTemplateId}", workItemTemplateId);
                return new List<WorkItem>();
            }
        }
        /// <inheritdoc />
        public async System.Threading.Tasks.Task<int> DeleteWorkItemTemplateWorkItemsAsync(string workItemTemplateId, string teamCode)
        {
            try
            {
                var filter = Builders<WorkItem>.Filter.And(
                    Builders<WorkItem>.Filter.Eq(s => s.RotationId, workItemTemplateId),
                    Builders<WorkItem>.Filter.Eq(s => s.TeamCode, teamCode)
                );

                var result = await _WorkItems.DeleteManyAsync(filter);
                return (int)result.DeletedCount;
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error deleting template WorkItems for Work Item Template {WorkItemTemplateId}", workItemTemplateId);
                throw;
            }
        }

        /// <summary>
        /// Normalizes worker-type text for consistent matching.
        /// </summary>
        private static string NormalizeWorkerType(string? workerType)
        {
            return string.IsNullOrWhiteSpace(workerType) ? "General" : workerType.Trim();
        }

        /// <summary>
        /// Returns the UTC start/end bounds for the Sunday-start week containing <paramref name="dateTimeUtc"/>.
        /// </summary>
        private static (DateTime WeekStartUtc, DateTime WeekEndUtc) GetWeekUtcBounds(DateTime dateTimeUtc)
        {
            var dayUtc = dateTimeUtc.Date;
            var weekStartUtc = dayUtc.AddDays(-(int)dayUtc.DayOfWeek);
            var weekEndUtc = weekStartUtc.AddDays(7);
            return (weekStartUtc, weekEndUtc);
        }

        /// <summary>
        /// Returns hours where an interval overlaps a containing window.
        /// </summary>
        private static double GetOverlapHours(DateTime intervalStartUtc, DateTime intervalEndUtc, DateTime windowStartUtc, DateTime windowEndUtc)
        {
            var overlapStart = intervalStartUtc > windowStartUtc ? intervalStartUtc : windowStartUtc;
            var overlapEnd = intervalEndUtc < windowEndUtc ? intervalEndUtc : windowEndUtc;
            return overlapEnd <= overlapStart ? 0 : (overlapEnd - overlapStart).TotalHours;
        }
    }
}
