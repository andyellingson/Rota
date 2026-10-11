using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Rota.Models;

namespace Rota.Services
{
    /// <summary>
    /// MongoDB-backed implementation of <see cref="ISchedulesService"/>.
    /// </summary>
    public class MongoSchedulesService : ISchedulesService
    {
        private readonly IMongoCollection<Schedule> _schedules;
        private readonly ILogger<MongoSchedulesService> _logger;
        private readonly IShiftsService? _shiftsService;
        private readonly IAbsencesService? _absencesService;
        private readonly IRemindersService? _remindersService;

        /// <summary>
        /// Matches schedules owned by manager ID or sharing the manager's code.
        /// </summary>
        private static FilterDefinition<Schedule> OwnerFilter(string managerId, string? managerCode)
        {
            var byManagerId = Builders<Schedule>.Filter.Eq(s => s.ManagerId, managerId);
            return string.IsNullOrEmpty(managerCode)
                ? byManagerId
                : Builders<Schedule>.Filter.Or(byManagerId, Builders<Schedule>.Filter.Eq(s => s.ManagerCode, managerCode));
        }

        /// <summary>
        /// Creates the schedules service using configured MongoDB options.
        /// </summary>
        public MongoSchedulesService(IOptions<MongoDbOptions> options, ILogger<MongoSchedulesService> logger, IShiftsService shiftsService, IAbsencesService absencesService, IRemindersService remindersService)
        {
            _logger = logger;
            _shiftsService = shiftsService;
            _absencesService = absencesService;
            _remindersService = remindersService;
            var opts = options?.Value ?? throw new ArgumentNullException(nameof(options));
            if (string.IsNullOrWhiteSpace(opts.ConnectionString))
            {
                throw new InvalidOperationException("MongoDb:ConnectionString is required.");
            }

            var client = new MongoClient(opts.ConnectionString);
            var db = client.GetDatabase(opts.DatabaseName);
            _schedules = db.GetCollection<Schedule>("schedules");
        }

        public MongoSchedulesService(IMongoCollection<Schedule> schedulesCollection, ILogger<MongoSchedulesService> logger, IShiftsService? shiftsService = null, IAbsencesService? absencesService = null, IRemindersService? remindersService = null)
        {
            _schedules = schedulesCollection;
            _logger = logger;
            _shiftsService = shiftsService;
            _absencesService = absencesService;
            _remindersService = remindersService;
        }

        /// <inheritdoc />
        public async Task<List<Schedule>> GetSchedulesForManagerAsync(string managerId, string? managerCode = null)
        {
            try
            {
                var filter = OwnerFilter(managerId, managerCode);
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

        /// <inheritdoc />
        public async Task<Schedule?> GetScheduleByIdAsync(string scheduleId, string managerId, string? managerCode = null)
        {
            try
            {
                var filter = Builders<Schedule>.Filter.And(
                    Builders<Schedule>.Filter.Eq(s => s.Id, scheduleId),
                    OwnerFilter(managerId, managerCode)
                );
                return await _schedules.Find(filter).FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving schedule {ScheduleId}", scheduleId);
                return null;
            }
        }

        /// <inheritdoc />
        public async Task<Schedule> CreateScheduleAsync(Schedule schedule)
        {
            try
            {
                // Check if a schedule with this name already exists for this manager
                var existingFilter = Builders<Schedule>.Filter.And(
                    OwnerFilter(schedule.ManagerId, schedule.ManagerCode),
                    Builders<Schedule>.Filter.Eq(s => s.Name, schedule.Name)
                );
                var existing = await _schedules.Find(existingFilter).FirstOrDefaultAsync();
                if (existing != null)
                {
                    throw new InvalidOperationException($"A schedule with the name '{schedule.Name}' already exists.");
                }

                schedule.CreatedAt = DateTime.UtcNow;
                
                // If this is the first schedule or marked as default, make it default
                var managerSchedules = await GetSchedulesForManagerAsync(schedule.ManagerId, schedule.ManagerCode);
                if (managerSchedules.Count == 0 || schedule.IsDefault)
                {
                    // Clear other defaults
                    if (schedule.IsDefault)
                    {
                        var updateFilter = Builders<Schedule>.Filter.And(
                            OwnerFilter(schedule.ManagerId, schedule.ManagerCode),
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

        /// <inheritdoc />
        public async Task<Schedule?> UpdateScheduleAsync(string scheduleId, string managerId, string name, string? description, bool isDefault, string? managerCode = null)
        {
            try
            {
                // Check if name is unique (excluding current schedule)
                var nameCheckFilter = Builders<Schedule>.Filter.And(
                    OwnerFilter(managerId, managerCode),
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
                        OwnerFilter(managerId, managerCode),
                        Builders<Schedule>.Filter.Ne(s => s.Id, scheduleId)
                    );
                    var clearUpdate = Builders<Schedule>.Update.Set(s => s.IsDefault, false);
                    await _schedules.UpdateManyAsync(clearDefaultFilter, clearUpdate);
                }

                var filter = Builders<Schedule>.Filter.And(
                    Builders<Schedule>.Filter.Eq(s => s.Id, scheduleId),
                    OwnerFilter(managerId, managerCode)
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

        /// <inheritdoc />
        public async Task<bool> DeleteScheduleAsync(string scheduleId, string managerId, string? managerCode = null)
        {
            try
            {
                // Check if this is the only schedule
                var managerSchedules = await GetSchedulesForManagerAsync(managerId, managerCode);
                if (managerSchedules.Count <= 1)
                {
                    throw new InvalidOperationException("Cannot delete the last schedule. At least one schedule must exist.");
                }

                var schedule = await GetScheduleByIdAsync(scheduleId, managerId, managerCode);
                if (schedule == null)
                {
                    return false;
                }

                var filter = Builders<Schedule>.Filter.And(
                    Builders<Schedule>.Filter.Eq(s => s.Id, scheduleId),
                    OwnerFilter(managerId, managerCode)
                );

                if (_shiftsService is null || _absencesService is null || _remindersService is null)
                {
                    throw new InvalidOperationException("The schedule could not be deleted because cleanup of its shifts, absences, and reminders is unavailable. The schedule was not deleted.");
                }

                // Delete associated data first; any failure aborts before the schedule itself is removed
                try
                {
                    await _shiftsService.DeleteShiftsByScheduleIdAsync(scheduleId);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException("The schedule could not be deleted because its shifts could not be deleted. The schedule was not deleted. Please try again.", ex);
                }

                try
                {
                    await _absencesService.DeleteAbsencesByScheduleIdAsync(scheduleId);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException("The schedule could not be deleted because its absences could not be deleted. The schedule was not deleted. Please try again.", ex);
                }

                try
                {
                    await _remindersService.DeleteRemindersByScheduleIdAsync(scheduleId);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException("The schedule could not be deleted because its reminders could not be deleted. The schedule was not deleted. Please try again.", ex);
                }

                var result = await _schedules.DeleteOneAsync(filter);
                if (result.DeletedCount == 0)
                {
                    return false;
                }

                // If we deleted the default schedule, set another one as default
                if (schedule.IsDefault)
                {
                    var remainingSchedules = await GetSchedulesForManagerAsync(managerId, managerCode);
                    if (remainingSchedules.Any())
                    {
                        await SetDefaultScheduleAsync(remainingSchedules.First().Id!, managerId, managerCode);
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting schedule {ScheduleId}", scheduleId);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<Schedule> GetOrCreateDefaultScheduleAsync(string managerId, string managerUsername, string? managerCode)
        {
            try
            {
                // Try to get default schedule
                var defaultFilter = Builders<Schedule>.Filter.And(
                    OwnerFilter(managerId, managerCode),
                    Builders<Schedule>.Filter.Eq(s => s.IsDefault, true)
                );
                var defaultSchedule = await _schedules.Find(defaultFilter).FirstOrDefaultAsync();
                if (defaultSchedule != null)
                {
                    return defaultSchedule;
                }

                // Try to get any schedule
                var anyFilter = OwnerFilter(managerId, managerCode);
                var anySchedule = await _schedules.Find(anyFilter).FirstOrDefaultAsync();
                if (anySchedule != null)
                {
                    // Set it as default
                    await SetDefaultScheduleAsync(anySchedule.Id!, managerId, managerCode);
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

        /// <inheritdoc />
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

        /// <inheritdoc />
        public async Task<bool> SetDefaultScheduleAsync(string scheduleId, string managerId, string? managerCode = null)
        {
            try
            {
                // Clear all defaults for this manager
                var clearFilter = OwnerFilter(managerId, managerCode);
                var clearUpdate = Builders<Schedule>.Update.Set(s => s.IsDefault, false);
                await _schedules.UpdateManyAsync(clearFilter, clearUpdate);

                // Set the new default
                var setFilter = Builders<Schedule>.Filter.And(
                    Builders<Schedule>.Filter.Eq(s => s.Id, scheduleId),
                    OwnerFilter(managerId, managerCode)
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
