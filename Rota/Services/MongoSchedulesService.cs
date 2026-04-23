using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Rota.Models;

namespace Rota.Services
{
    public class MongoSchedulesService : ISchedulesService
    {
        private readonly IMongoCollection<Schedule> _schedules;
        private readonly ILogger<MongoSchedulesService> _logger;

        public MongoSchedulesService(IOptions<MongoDbOptions> options, ILogger<MongoSchedulesService> logger)
        {
            _logger = logger;
            var opts = options.Value;
            var client = new MongoClient(opts.ConnectionString);
            var db = client.GetDatabase(opts.DatabaseName);
            _schedules = db.GetCollection<Schedule>("schedules");
        }

        public async Task<Rotation> AddRotationAsync(string scheduleId, string managerId, Rotation rotation)
        {
            try
            {
                // Ensure schedule belongs to manager
                var filter = Builders<Schedule>.Filter.And(
                    Builders<Schedule>.Filter.Eq(s => s.Id, scheduleId),
                    Builders<Schedule>.Filter.Eq(s => s.ManagerId, managerId)
                );

                // Assign id if missing
                if (string.IsNullOrEmpty(rotation.Id)) rotation.Id = MongoDB.Bson.ObjectId.GenerateNewId().ToString();

                var update = Builders<Schedule>.Update.Push(s => s.Rotations, rotation);
                var result = await _schedules.UpdateOneAsync(filter, update);
                if (result.ModifiedCount > 0)
                {
                    return rotation;
                }

                throw new InvalidOperationException("Unable to add rotation (schedule not found or not authorized).");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding rotation to schedule {ScheduleId}", scheduleId);
                throw;
            }
        }

        public async Task<bool> DeleteRotationAsync(string scheduleId, string managerId, string rotationId)
        {
            try
            {
                var filter = Builders<Schedule>.Filter.And(
                    Builders<Schedule>.Filter.Eq(s => s.Id, scheduleId),
                    Builders<Schedule>.Filter.Eq(s => s.ManagerId, managerId)
                );

                var update = Builders<Schedule>.Update.PullFilter(s => s.Rotations, Builders<Rotation>.Filter.Eq(w => w.Id, rotationId));
                var result = await _schedules.UpdateOneAsync(filter, update);
                return result.ModifiedCount > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting rotation {RotationId} from schedule {ScheduleId}", rotationId, scheduleId);
                throw;
            }
        }

        public async Task<List<Schedule>> GetSchedulesForManagerAsync(string managerId)
        {
            try
            {
                var filter = Builders<Schedule>.Filter.Eq(s => s.ManagerId, managerId);
                return await _schedules.Find(filter)
                    .SortBy(s => s.CreatedAt)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving schedules for manager {ManagerId}", managerId);
                return new List<Schedule>();
            }
        }

        public async Task<Schedule?> GetScheduleByIdAsync(string scheduleId, string managerId)
        {
            try
            {
                var filter = Builders<Schedule>.Filter.And(
                    Builders<Schedule>.Filter.Eq(s => s.Id, scheduleId),
                    Builders<Schedule>.Filter.Eq(s => s.ManagerId, managerId)
                );
                return await _schedules.Find(filter).FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving schedule {ScheduleId}", scheduleId);
                return null;
            }
        }

        public async Task<Schedule> CreateScheduleAsync(Schedule schedule)
        {
            try
            {
                // Check if a schedule with this name already exists for this manager
                var existingFilter = Builders<Schedule>.Filter.And(
                    Builders<Schedule>.Filter.Eq(s => s.ManagerId, schedule.ManagerId),
                    Builders<Schedule>.Filter.Eq(s => s.Name, schedule.Name)
                );
                var existing = await _schedules.Find(existingFilter).FirstOrDefaultAsync();
                if (existing != null)
                {
                    throw new InvalidOperationException($"A schedule with the name '{schedule.Name}' already exists.");
                }

                schedule.CreatedAt = DateTime.UtcNow;
                
                // If this is the first schedule or marked as default, make it default
                var managerSchedules = await GetSchedulesForManagerAsync(schedule.ManagerId);
                if (managerSchedules.Count == 0 || schedule.IsDefault)
                {
                    // Clear other defaults
                    if (schedule.IsDefault)
                    {
                        var updateFilter = Builders<Schedule>.Filter.And(
                            Builders<Schedule>.Filter.Eq(s => s.ManagerId, schedule.ManagerId),
                            Builders<Schedule>.Filter.Eq(s => s.IsDefault, true)
                        );
                        var update = Builders<Schedule>.Update.Set(s => s.IsDefault, false);
                        await _schedules.UpdateManyAsync(updateFilter, update);
                    }
                    schedule.IsDefault = true;
                }

                await _schedules.InsertOneAsync(schedule);
                return schedule;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating schedule '{Name}' for manager {ManagerId}", schedule.Name, schedule.ManagerId);
                throw;
            }
        }

        public async Task<Schedule?> UpdateScheduleAsync(string scheduleId, string managerId, string name, string? description, bool isDefault)
        {
            try
            {
                // Check if name is unique (excluding current schedule)
                var nameCheckFilter = Builders<Schedule>.Filter.And(
                    Builders<Schedule>.Filter.Eq(s => s.ManagerId, managerId),
                    Builders<Schedule>.Filter.Eq(s => s.Name, name),
                    Builders<Schedule>.Filter.Ne(s => s.Id, scheduleId)
                );
                var nameExists = await _schedules.Find(nameCheckFilter).FirstOrDefaultAsync();
                if (nameExists != null)
                {
                    throw new InvalidOperationException($"A schedule with the name '{name}' already exists.");
                }

                // If setting as default, clear other defaults
                if (isDefault)
                {
                    var clearDefaultFilter = Builders<Schedule>.Filter.And(
                        Builders<Schedule>.Filter.Eq(s => s.ManagerId, managerId),
                        Builders<Schedule>.Filter.Ne(s => s.Id, scheduleId)
                    );
                    var clearUpdate = Builders<Schedule>.Update.Set(s => s.IsDefault, false);
                    await _schedules.UpdateManyAsync(clearDefaultFilter, clearUpdate);
                }

                var filter = Builders<Schedule>.Filter.And(
                    Builders<Schedule>.Filter.Eq(s => s.Id, scheduleId),
                    Builders<Schedule>.Filter.Eq(s => s.ManagerId, managerId)
                );

                var update = Builders<Schedule>.Update
                    .Set(s => s.Name, name)
                    .Set(s => s.Description, description)
                    .Set(s => s.IsDefault, isDefault);

                var options = new FindOneAndUpdateOptions<Schedule>
                {
                    ReturnDocument = ReturnDocument.After
                };

                return await _schedules.FindOneAndUpdateAsync(filter, update, options);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating schedule {ScheduleId}", scheduleId);
                throw;
            }
        }

        public async Task<bool> DeleteScheduleAsync(string scheduleId, string managerId)
        {
            try
            {
                // Check if this is the only schedule
                var managerSchedules = await GetSchedulesForManagerAsync(managerId);
                if (managerSchedules.Count <= 1)
                {
                    throw new InvalidOperationException("Cannot delete the last schedule. At least one schedule must exist.");
                }

                var schedule = await GetScheduleByIdAsync(scheduleId, managerId);
                if (schedule == null)
                {
                    return false;
                }

                var filter = Builders<Schedule>.Filter.And(
                    Builders<Schedule>.Filter.Eq(s => s.Id, scheduleId),
                    Builders<Schedule>.Filter.Eq(s => s.ManagerId, managerId)
                );

                var result = await _schedules.DeleteOneAsync(filter);

                // If we deleted the default schedule, set another one as default
                if (result.DeletedCount > 0 && schedule.IsDefault)
                {
                    var remainingSchedules = await GetSchedulesForManagerAsync(managerId);
                    if (remainingSchedules.Any())
                    {
                        await SetDefaultScheduleAsync(remainingSchedules.First().Id!, managerId);
                    }
                }

                return result.DeletedCount > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting schedule {ScheduleId}", scheduleId);
                throw;
            }
        }

        public async Task<Schedule> GetOrCreateDefaultScheduleAsync(string managerId, string managerUsername, string? managerCode)
        {
            try
            {
                // Try to get default schedule
                var defaultFilter = Builders<Schedule>.Filter.And(
                    Builders<Schedule>.Filter.Eq(s => s.ManagerId, managerId),
                    Builders<Schedule>.Filter.Eq(s => s.IsDefault, true)
                );
                var defaultSchedule = await _schedules.Find(defaultFilter).FirstOrDefaultAsync();
                if (defaultSchedule != null)
                {
                    return defaultSchedule;
                }

                // Try to get any schedule
                var anyFilter = Builders<Schedule>.Filter.Eq(s => s.ManagerId, managerId);
                var anySchedule = await _schedules.Find(anyFilter).FirstOrDefaultAsync();
                if (anySchedule != null)
                {
                    // Set it as default
                    await SetDefaultScheduleAsync(anySchedule.Id!, managerId);
                    return anySchedule;
                }

                // Create a default schedule
                var newSchedule = new Schedule
                {
                    Name = "Default Schedule",
                    ManagerId = managerId,
                    ManagerUsername = managerUsername,
                    ManagerCode = managerCode,
                    IsDefault = true,
                    Description = "Your default schedule"
                };

                return await CreateScheduleAsync(newSchedule);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting or creating default schedule for manager {ManagerId}", managerId);
                throw;
            }
        }

        public async Task<List<Schedule>> GetSchedulesByIdsAsync(IEnumerable<string> scheduleIds)
        {
            try
            {
                var ids = scheduleIds.ToList();
                if (ids.Count == 0) return new List<Schedule>();
                var filter = Builders<Schedule>.Filter.In(s => s.Id, ids);
                return await _schedules.Find(filter).SortBy(s => s.Name).ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving schedules by IDs");
                return new List<Schedule>();
            }
        }

        public async Task<bool> SetDefaultScheduleAsync(string scheduleId, string managerId)
        {
            try
            {
                // Clear all defaults for this manager
                var clearFilter = Builders<Schedule>.Filter.Eq(s => s.ManagerId, managerId);
                var clearUpdate = Builders<Schedule>.Update.Set(s => s.IsDefault, false);
                await _schedules.UpdateManyAsync(clearFilter, clearUpdate);

                // Set the new default
                var setFilter = Builders<Schedule>.Filter.And(
                    Builders<Schedule>.Filter.Eq(s => s.Id, scheduleId),
                    Builders<Schedule>.Filter.Eq(s => s.ManagerId, managerId)
                );
                var setUpdate = Builders<Schedule>.Update.Set(s => s.IsDefault, true);
                var result = await _schedules.UpdateOneAsync(setFilter, setUpdate);

                return result.ModifiedCount > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting default schedule {ScheduleId}", scheduleId);
                return false;
            }
        }
    }
}
