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

        /// <summary>
        /// Gets all reminders for the specified user within a date range (inclusive).
        /// </summary>
        public async System.Threading.Tasks.Task<List<Reminder>> GetRemindersAsync(string username, DateOnly startDate, DateOnly endDate)
        {
            try
            {
                // Convert DateOnly to DateTime for MongoDB query
                var start = startDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
                var end = endDate.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);

                var filter = Builders<Reminder>.Filter.And(
                    Builders<Reminder>.Filter.Eq(r => r.Username, username),
                    Builders<Reminder>.Filter.Gte(r => r.Date, start),
                    Builders<Reminder>.Filter.Lte(r => r.Date, end)
                );

                return await _reminders.Find(filter).ToListAsync();
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error retrieving reminders for user {User} between {Start} and {End}", username, startDate, endDate);
                return new List<Reminder>();
            }
        }

        /// <summary>
        /// Fetches reminders matching the manager code and/or the personal owner ObjectId.
        /// An OR filter is used so both shared (manager) and personal reminders are returned.
        /// </summary>
        public async System.Threading.Tasks.Task<List<Reminder>> GetCalendarRemindersAsync(string? managerCode, string? ownerId, DateOnly startDate, DateOnly endDate)
        {
            try
            {
                var start = startDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
                var end = endDate.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);

                var dateFilter = Builders<Reminder>.Filter.And(
                    Builders<Reminder>.Filter.Gte(r => r.Date, start),
                    Builders<Reminder>.Filter.Lte(r => r.Date, end)
                );

                var userFilters = new List<FilterDefinition<Reminder>>();
                if (!string.IsNullOrEmpty(managerCode))
                    userFilters.Add(Builders<Reminder>.Filter.Eq(r => r.ManagerCode, managerCode));
                if (!string.IsNullOrEmpty(ownerId))
                    userFilters.Add(Builders<Reminder>.Filter.Eq(r => r.OwnerId, ownerId));

                if (userFilters.Count == 0) return new List<Reminder>();

                var userFilter = userFilters.Count == 1
                    ? userFilters[0]
                    : Builders<Reminder>.Filter.Or(userFilters);

                return await _reminders.Find(
                    Builders<Reminder>.Filter.And(dateFilter, userFilter)).ToListAsync();
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
                _logger.LogError(ex, "Error creating reminder for user {User}", reminder?.Username);
                throw;
            }
        }

        /// <summary>
        /// Deletes a reminder by ID (only if it belongs to the specified user).
        /// Returns true if the reminder was deleted; false if not found or not owned by user.
        /// </summary>
        public async System.Threading.Tasks.Task<bool> DeleteReminderAsync(string id, string username)
        {
            try
            {
                var filter = Builders<Reminder>.Filter.And(
                    Builders<Reminder>.Filter.Eq(r => r.Id, id),
                    Builders<Reminder>.Filter.Eq(r => r.Username, username)
                );

                var result = await _reminders.DeleteOneAsync(filter);
                return result.DeletedCount > 0;
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error deleting reminder {Id} for user {User}", id, username);
                return false;
            }
        }

        /// <summary>
        /// Updates an existing reminder (only if it belongs to the specified user).
        /// Returns the updated reminder if successful; null if not found or not owned by user.
        /// </summary>
        public async System.Threading.Tasks.Task<Reminder?> UpdateReminderAsync(string id, string username, string title, string? notes, DateTime reminderDateTimeUtc, string? color, string? ownerId, string? forUsername, string? forDisplayName, string? managerCode)
        {
            try
            {
                var filter = Builders<Reminder>.Filter.And(
                    Builders<Reminder>.Filter.Eq(r => r.Id, id),
                    Builders<Reminder>.Filter.Eq(r => r.Username, username)
                );

                var update = Builders<Reminder>.Update
                    .Set(r => r.Title, title)
                    .Set(r => r.Notes, notes)
                    .Set(r => r.Date, DateTime.SpecifyKind(reminderDateTimeUtc, DateTimeKind.Utc))
                    .Set(r => r.Color, color ?? "#ffeb3b")
                    .Set(r => r.OwnerId, ownerId)
                    .Set(r => r.ForUsername, forUsername)
                    .Set(r => r.ForDisplayName, forDisplayName)
                    .Set(r => r.ManagerCode, managerCode);

                var options = new FindOneAndUpdateOptions<Reminder> { ReturnDocument = ReturnDocument.After };
                var updated = await _reminders.FindOneAndUpdateAsync(filter, update, options);
                return updated;
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error updating reminder {Id} for user {User}", id, username);
                return null;
            }
        }
    }
}
