using Rota.Models;

namespace Rota.Services
{
    /// <summary>
    /// Service contract for managing shifts and shift templates.
    /// </summary>
    public interface IShiftsService
    {
        /// <summary>
        /// Returns shifts matching the supplied user scope and date window.
        /// </summary>
        System.Threading.Tasks.Task<List<Shift>> GetShiftsAsync(string? username, string? userId, string? managerCode, DateOnly startDate, DateOnly endDate);

        /// <summary>
        /// Creates a new shift.
        /// </summary>
        System.Threading.Tasks.Task<Shift> CreateShiftAsync(Shift shift);

        /// <summary>
        /// Deletes a shift owned by the specified username.
        /// </summary>
        System.Threading.Tasks.Task<bool> DeleteShiftAsync(string id, string username);

        /// <summary>
        /// Deletes all shifts in a recurring series for the specified user.
        /// </summary>
        System.Threading.Tasks.Task<int> DeleteShiftsBySeriesIdAsync(Guid seriesId, string username);

        /// <summary>
        /// Updates an existing shift owned by the specified username.
        /// </summary>
        System.Threading.Tasks.Task<Shift?> UpdateShiftAsync(string id, string username, DateTime startUtc, DateTime endUtc, string? title, string? notes, string workerType, string? color, string? assignedToUserId);

        /// <summary>
        /// Returns the distinct ScheduleIds of every shift the given user is assigned to.
        /// </summary>
        System.Threading.Tasks.Task<List<string>> GetDistinctScheduleIdsForUserAsync(string userId);

        /// <summary>
        /// Gets all template shifts for a specific Rotation ID.
        /// These are shifts with RotationId set and are not tied to specific calendar dates.
        /// </summary>
        System.Threading.Tasks.Task<List<Shift>> GetRotationTemplateShiftsAsync(string rotationId, string managerCode);

        /// <summary>
        /// Deletes all template shifts associated with a specific Rotation ID.
        /// </summary>
        System.Threading.Tasks.Task<int> DeleteRotationTemplateShiftsAsync(string rotationId, string managerCode);
    }
}
