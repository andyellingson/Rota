using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Rota.Models;

namespace Rota.Services
{
    /// <summary>
    /// MongoDB-backed implementation of <see cref="IRemindersService"/>.
    /// </summary>
    public class MongoRemindersService : IRemindersService
    {
        private readonly IMongoCollection<Reminder> _reminders;
        private readonly ILogger<MongoRemindersService> _logger;

        /// <summary>
        /// Production constructor which creates a MongoDB collection reference using provided options.
        /// </summary>
        public MongoRemindersService(IOptions<MongoDbOptions> options, ILogger<MongoRemindersService> logger)
        {
            _logger = logger;
            var opts = options.Value;

            try
            {
                var client = new MongoClient(opts.ConnectionString);
                var db = client.GetDatabase(opts.DatabaseName);
                _reminders = db.GetCollection<Reminder>(opts.RemindersCollectionName);
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error initializing MongoRemindersService");
                throw;
            }
        }

        /// <summary>
        /// Test-friendly constructor that accepts an <see cref="IMongoCollection{Reminder}"/> directly.
        /// </summary>
        public MongoRemindersService(IMongoCollection<Reminder> remindersCollection, ILogger<MongoRemindersService> logger)
        {
            _reminders = remindersCollection;
            _logger = logger;
        }

        /// <inheritdoc />
        public async System.Threading.Tasks.Task<long> DeleteRemindersByScheduleIdAsync(string scheduleId)
        {
            try
            {
                var result = await _reminders.DeleteManyAsync(Builders<Reminder>.Filter.Eq(r => r.ScheduleId, scheduleId));
                return result.DeletedCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting reminders for schedule {ScheduleId}", scheduleId);
                throw;
            }
        }

        /// <summary>
        /// Fetches public team reminders and private reminders for the recipient or team manager.
        /// </summary>
        public async System.Threading.Tasks.Task<List<Reminder>> GetCalendarRemindersAsync(string? teamCode, string? ownerId, DateOnly startDate, DateOnly endDate, bool isManager = false)
        {
            try
            {
                var start = startDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
                var end = endDate.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);

                var dateFilter = Builders<Reminder>.Filter.And(
                    Builders<Reminder>.Filter.Gte(r => r.Date, start),
                    Builders<Reminder>.Filter.Lte(r => r.Date, end)
                );

                var visibilityFilters = new List<FilterDefinition<Reminder>>();
                if (!string.IsNullOrEmpty(teamCode))
                {
                    var isLegacyPublic = Builders<Reminder>.Filter.Exists("visibility", false);
                    var isPublic = Builders<Reminder>.Filter.Or(
                        Builders<Reminder>.Filter.Eq(r => r.Visibility, ReminderVisibility.Public),
                        isLegacyPublic);
                    visibilityFilters.Add(Builders<Reminder>.Filter.And(
                        Builders<Reminder>.Filter.Eq(r => r.TeamCode, teamCode),
                        isPublic));

                    if (isManager)
                    {
                        visibilityFilters.Add(Builders<Reminder>.Filter.And(
                            Builders<Reminder>.Filter.Eq(r => r.TeamCode, teamCode),
                            Builders<Reminder>.Filter.Eq(r => r.Visibility, ReminderVisibility.Private)));
                    }
                }

                if (!string.IsNullOrEmpty(ownerId))
                {
                    var isLegacyReminder = Builders<Reminder>.Filter.Exists("visibility", false);
                    visibilityFilters.Add(Builders<Reminder>.Filter.And(
                        Builders<Reminder>.Filter.Eq(r => r.OwnerId, ownerId),
                        Builders<Reminder>.Filter.Or(
                            Builders<Reminder>.Filter.Eq(r => r.Visibility, ReminderVisibility.Private),
                            isLegacyReminder)));
                }

                if (visibilityFilters.Count == 0) return new List<Reminder>();

                var visibilityFilter = Builders<Reminder>.Filter.Or(visibilityFilters);

                return await _reminders.Find(
                    Builders<Reminder>.Filter.And(dateFilter, visibilityFilter)).ToListAsync();
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error retrieving calendar reminders");
                return new List<Reminder>();
            }
        }

        /// <summary>
        /// Creates a new reminder for the specified user.
        /// </summary>
        public async System.Threading.Tasks.Task<Reminder> CreateReminderAsync(Reminder reminder)
        {
            try
            {
                reminder.CreatedAt = DateTime.UtcNow;
                await _reminders.InsertOneAsync(reminder);
                return reminder;
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error creating reminder for user {User}", reminder?.CreatedByUserId);
                throw;
            }
        }

        /// <summary>
        /// Deletes a reminder when the requester created it or is its team manager.
        /// </summary>
        public async System.Threading.Tasks.Task<bool> DeleteReminderAsync(string id, string requesterId, bool isManager = false, string? teamCode = null)
        {
            try
            {
                var accessFilter = Builders<Reminder>.Filter.Or(
                    Builders<Reminder>.Filter.Eq(r => r.CreatedByUserId, requesterId),
                    Builders<Reminder>.Filter.And(
                        Builders<Reminder>.Filter.Eq(r => r.OwnerId, requesterId),
                        Builders<Reminder>.Filter.Or(
                            Builders<Reminder>.Filter.Eq(r => r.Visibility, ReminderVisibility.Private),
                            Builders<Reminder>.Filter.Exists("visibility", false))));
                if (isManager && !string.IsNullOrEmpty(teamCode))
                {
                    accessFilter = Builders<Reminder>.Filter.Or(
                        accessFilter,
                        Builders<Reminder>.Filter.Eq(r => r.TeamCode, teamCode));
                }
                var filter = Builders<Reminder>.Filter.And(Builders<Reminder>.Filter.Eq(r => r.Id, id), accessFilter);

                var result = await _reminders.DeleteOneAsync(filter);
                return result.DeletedCount > 0;
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error deleting reminder {Id} for user {User}", id, requesterId);
                return false;
            }
        }

        /// <summary>
        /// Updates a reminder when the requester created it or is its team manager.
        /// </summary>
        public async System.Threading.Tasks.Task<Reminder?> UpdateReminderAsync(string id, string requesterId, bool isManager, string? teamCode, string title, string? notes, DateTime reminderDateTimeUtc, string? color, string? ownerId, string? forUsername, string? forDisplayName, ReminderVisibility visibility)
        {
            try
            {
                var accessFilter = Builders<Reminder>.Filter.Or(
                    Builders<Reminder>.Filter.Eq(r => r.CreatedByUserId, requesterId),
                    Builders<Reminder>.Filter.And(
                        Builders<Reminder>.Filter.Eq(r => r.OwnerId, requesterId),
                        Builders<Reminder>.Filter.Or(
                            Builders<Reminder>.Filter.Eq(r => r.Visibility, ReminderVisibility.Private),
                            Builders<Reminder>.Filter.Exists("visibility", false))));
                if (isManager && !string.IsNullOrEmpty(teamCode))
                {
                    accessFilter = Builders<Reminder>.Filter.Or(
                        accessFilter,
                        Builders<Reminder>.Filter.Eq(r => r.TeamCode, teamCode));
                }
                var filter = Builders<Reminder>.Filter.And(Builders<Reminder>.Filter.Eq(r => r.Id, id), accessFilter);

                var update = Builders<Reminder>.Update
                    .Set(r => r.Title, title)
                    .Set(r => r.Notes, notes)
                    .Set(r => r.Date, DateTime.SpecifyKind(reminderDateTimeUtc, DateTimeKind.Utc))
                    .Set(r => r.Color, color ?? "#ffeb3b")
                    .Set(r => r.OwnerId, ownerId)
                    .Set(r => r.ForUsername, forUsername)
                    .Set(r => r.ForDisplayName, forDisplayName)
                    .Set(r => r.TeamCode, teamCode)
                    .Set(r => r.Visibility, visibility);

                var options = new FindOneAndUpdateOptions<Reminder> { ReturnDocument = ReturnDocument.After };
                var updated = await _reminders.FindOneAndUpdateAsync(filter, update, options);
                return updated;
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error updating reminder {Id} for user {User}", id, requesterId);
                return null;
            }
        }
    }
}
