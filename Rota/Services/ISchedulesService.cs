using Rota.Models;

namespace Rota.Services
{
    /// <summary>
    /// Service for managing schedules.
    /// </summary>
    public interface ISchedulesService
    {
        /// <summary>
        /// Gets all schedules for a manager.
        /// </summary>
        Task<List<Schedule>> GetSchedulesForManagerAsync(string managerId);

        /// <summary>
        /// Gets a specific schedule by ID.
        /// </summary>
        Task<Schedule?> GetScheduleByIdAsync(string scheduleId, string managerId);

        /// <summary>
        /// Creates a new schedule.
        /// </summary>
        Task<Schedule> CreateScheduleAsync(Schedule schedule);

        /// <summary>
        /// Updates an existing schedule.
        /// </summary>
        Task<Schedule?> UpdateScheduleAsync(string scheduleId, string managerId, string name, string? description, bool isDefault);

        /// <summary>
        /// Deletes a schedule and optionally all associated events.
        /// </summary>
        Task<bool> DeleteScheduleAsync(string scheduleId, string managerId);

        /// <summary>
        /// Gets the default schedule for a manager, or creates one if none exists.
        /// </summary>
        Task<Schedule> GetOrCreateDefaultScheduleAsync(string managerId, string managerUsername, string? managerCode);

        /// <summary>
        /// Sets a schedule as the default for a manager.
        /// </summary>
        Task<bool> SetDefaultScheduleAsync(string scheduleId, string managerId);

        /// <summary>
        /// Returns the schedules whose IDs are in the supplied list (used to look up
        /// schedules an employee is assigned to across multiple managers).
        /// </summary>
        Task<List<Schedule>> GetSchedulesByIdsAsync(IEnumerable<string> scheduleIds);

        /// <summary>
        /// Adds a new Rotation template to the specified schedule.
        /// </summary>
        Task<Rotation> AddRotationAsync(string scheduleId, string managerId, Rotation rotation);

        /// <summary>
        /// Deletes a Rotation template from the specified schedule.
        /// </summary>
        Task<bool> DeleteRotationAsync(string scheduleId, string managerId, string rotationId);
    }
}
