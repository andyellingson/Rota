using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Rota.Models;

namespace Rota.Services
{
    public class MongoShiftsService : IShiftsService
    {
        private readonly IMongoCollection<Shift> _shifts;
        private readonly ILogger<MongoShiftsService> _logger;

        public MongoShiftsService(IOptions<MongoDbOptions> options, ILogger<MongoShiftsService> logger)
        {
            _logger = logger;
            var opts = options.Value;
            var client = new MongoClient(opts.ConnectionString);
            var db = client.GetDatabase(opts.DatabaseName);
            _shifts = db.GetCollection<Shift>(opts.ShiftsCollectionName);
        }

        public MongoShiftsService(IMongoCollection<Shift> shiftsCollection, ILogger<MongoShiftsService> logger)
        {
            _shifts = shiftsCollection;
            _logger = logger;
        }

        public async System.Threading.Tasks.Task<List<Shift>> GetShiftsAsync(string? username, string? userId, string? managerCode, DateOnly startDate, DateOnly endDate)
        {
            try
            {
                var start = startDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
                var end = endDate.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);
                FilterDefinition<Shift> userFilter;
                if (!string.IsNullOrEmpty(managerCode))
                {
                    // Primary: query by ManagerCode to return the full manager roster
                    userFilter = Builders<Shift>.Filter.Eq(s => s.ManagerCode, managerCode);
                }
                else if (!string.IsNullOrEmpty(userId) || !string.IsNullOrEmpty(username))
                {
                    // Fallback: return shifts either created by the username or assigned to the userId
                    var filters = new List<FilterDefinition<Shift>>();
                    if (!string.IsNullOrEmpty(username))
                        filters.Add(Builders<Shift>.Filter.Eq(s => s.Username, username));
                    if (!string.IsNullOrEmpty(userId))
                        filters.Add(Builders<Shift>.Filter.Eq(s => s.AssignedToUserId, userId));

                    userFilter = filters.Count == 1 ? filters[0] : Builders<Shift>.Filter.Or(filters);
                }
                else
                {
                    return new List<Shift>();
                }

                var filter = Builders<Shift>.Filter.And(
                    userFilter,
                    Builders<Shift>.Filter.Gte(s => s.Start, start),
                    Builders<Shift>.Filter.Lte(s => s.End, end)
                );

                return await _shifts.Find(filter).ToListAsync();
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error retrieving shifts between {Start} and {End}", startDate, endDate);
                return new List<Shift>();
            }
        }

        public async System.Threading.Tasks.Task<Shift> CreateShiftAsync(Shift shift)
        {
            try
            {
                shift.CreatedAt = DateTime.UtcNow;
                await _shifts.InsertOneAsync(shift);
                return shift;
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error creating shift for user {User}", shift?.Username);
                throw;
            }
        }

        public async System.Threading.Tasks.Task<bool> DeleteShiftAsync(string id, string username)
        {
            try
            {
                var filter = Builders<Shift>.Filter.And(
                    Builders<Shift>.Filter.Eq(s => s.Id, id),
                    Builders<Shift>.Filter.Eq(s => s.Username, username)
                );

                var result = await _shifts.DeleteOneAsync(filter);
                return result.DeletedCount > 0;
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error deleting shift {Id} for user {User}", id, username);
                return false;
            }
        }

        public async System.Threading.Tasks.Task<int> DeleteShiftsBySeriesIdAsync(Guid seriesId, string username)
        {
            try
            {
                var filter = Builders<Shift>.Filter.And(
                    Builders<Shift>.Filter.Eq(s => s.SeriesId, seriesId),
                    Builders<Shift>.Filter.Eq(s => s.Username, username)
                );

                var result = await _shifts.DeleteManyAsync(filter);
                return (int)result.DeletedCount;
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error deleting shifts with seriesId {SeriesId} for user {User}", seriesId, username);
                return 0;
            }
        }

        public async System.Threading.Tasks.Task<List<string>> GetDistinctScheduleIdsForUserAsync(string userId)
        {
            try
            {
                var filter = Builders<Shift>.Filter.And(
                    Builders<Shift>.Filter.Eq(s => s.AssignedToUserId, userId),
                    Builders<Shift>.Filter.Ne(s => s.ScheduleId, null)
                );
                var ids = await _shifts.Distinct(s => s.ScheduleId, filter).ToListAsync();
                return ids.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToList();
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error getting distinct schedule IDs for user {UserId}", userId);
                return new List<string>();
            }
        }

        public async System.Threading.Tasks.Task<Shift?> UpdateShiftAsync(string id, string username, DateTime startUtc, DateTime endUtc, string? title, string? notes, WorkerType workerType, string? color, string? assignedToUserId)
        {
            try
            {
                var filter = Builders<Shift>.Filter.And(
                    Builders<Shift>.Filter.Eq(s => s.Id, id),
                    Builders<Shift>.Filter.Eq(s => s.Username, username)
                );

                var update = Builders<Shift>.Update
                    .Set(s => s.Start, DateTime.SpecifyKind(startUtc, DateTimeKind.Utc))
                    .Set(s => s.End, DateTime.SpecifyKind(endUtc, DateTimeKind.Utc))
                    .Set(s => s.Title, title)
                    .Set(s => s.Notes, notes)
                    .Set(s => s.WorkerType, workerType)
                    .Set(s => s.Color, color)
                    .Set(s => s.AssignedToUserId, assignedToUserId);

                var options = new FindOneAndUpdateOptions<Shift> { ReturnDocument = ReturnDocument.After };
                var updated = await _shifts.FindOneAndUpdateAsync(filter, update, options);
                return updated;
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error updating shift {Id} for user {User}", id, username);
                return null;
            }
        }

        public async System.Threading.Tasks.Task<List<Shift>> GetRotationTemplateShiftsAsync(string rotationId, string managerCode)
        {
            try
            {
                var filter = Builders<Shift>.Filter.And(
                    Builders<Shift>.Filter.Eq(s => s.RotationId, rotationId),
                    Builders<Shift>.Filter.Eq(s => s.ManagerCode, managerCode)
                );

                var templateShifts = await _shifts.Find(filter).ToListAsync();
                return templateShifts;
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error getting template shifts for Rotation {RotationId}", rotationId);
                return new List<Shift>();
            }
        }

        public async System.Threading.Tasks.Task<int> DeleteRotationTemplateShiftsAsync(string rotationId, string managerCode)
        {
            try
            {
                var filter = Builders<Shift>.Filter.And(
                    Builders<Shift>.Filter.Eq(s => s.RotationId, rotationId),
                    Builders<Shift>.Filter.Eq(s => s.ManagerCode, managerCode)
                );

                var result = await _shifts.DeleteManyAsync(filter);
                return (int)result.DeletedCount;
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error deleting template shifts for Rotation {RotationId}", rotationId);
                return 0;
            }
        }
    }
}
