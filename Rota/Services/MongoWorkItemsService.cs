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
        private readonly ILogger<MongoWorkItemsService> _logger;

        /// <summary>
        /// Creates the service using configured MongoDB options.
        /// </summary>
        public MongoWorkItemsService(IOptions<MongoDbOptions> options, ILogger<MongoWorkItemsService> logger)
        {
            _logger = logger;
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
        public MongoWorkItemsService(IMongoCollection<WorkItem> WorkItemsCollection, ILogger<MongoWorkItemsService> logger)
        {
            _WorkItems = WorkItemsCollection;
            _logger = logger;
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
        public async System.Threading.Tasks.Task<WorkItem?> UpdateWorkItemAsync(string id, string username, DateTime startUtc, DateTime endUtc, string? title, string? notes, string workerType, string? color, string? assignedToUserId)
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
                    .Set(s => s.AssignedToUserId, assignedToUserId);

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
        public async System.Threading.Tasks.Task<List<WorkItem>> GetRotationTemplateWorkItemsAsync(string rotationId, string teamCode)
        {
            try
            {
                var filter = Builders<WorkItem>.Filter.And(
                    Builders<WorkItem>.Filter.Eq(s => s.RotationId, rotationId),
                    Builders<WorkItem>.Filter.Eq(s => s.TeamCode, teamCode)
                );

                var templateWorkItems = await _WorkItems.Find(filter).ToListAsync();
                return templateWorkItems;
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error getting template WorkItems for Rotation {RotationId}", rotationId);
                return new List<WorkItem>();
            }
        }

        /// <inheritdoc />
        public async System.Threading.Tasks.Task<int> DeleteRotationTemplateWorkItemsAsync(string rotationId, string teamCode)
        {
            try
            {
                var filter = Builders<WorkItem>.Filter.And(
                    Builders<WorkItem>.Filter.Eq(s => s.RotationId, rotationId),
                    Builders<WorkItem>.Filter.Eq(s => s.TeamCode, teamCode)
                );

                var result = await _WorkItems.DeleteManyAsync(filter);
                return (int)result.DeletedCount;
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error deleting template WorkItems for Rotation {RotationId}", rotationId);
                throw;
            }
        }
    }
}
