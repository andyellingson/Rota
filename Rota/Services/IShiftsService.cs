using Rota.Models;

namespace Rota.Services
{
    public interface IShiftsService
    {
        System.Threading.Tasks.Task<List<Shift>> GetShiftsAsync(string? username, string? userId, string? managerCode, DateOnly startDate, DateOnly endDate);
        System.Threading.Tasks.Task<Shift> CreateShiftAsync(Shift shift);
        System.Threading.Tasks.Task<bool> DeleteShiftAsync(string id, string username);
        System.Threading.Tasks.Task<int> DeleteShiftsBySeriesIdAsync(Guid seriesId, string username);
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
