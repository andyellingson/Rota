using Rota.Models;

namespace Rota.Services
{
    /// <summary>
    /// Service interface for managing user reminders.
    /// </summary>
    public interface IRemindersService
    {
        /// <summary>
        /// Gets all reminders for the specified user within a date range (inclusive).
        /// </summary>
        System.Threading.Tasks.Task<List<Reminder>> GetRemindersAsync(string username, DateOnly startDate, DateOnly endDate);

        /// <summary>
        /// Fetches calendar reminders using the manager code (for shared roster reminders)
        /// and/or the owner's ObjectId (for personal reminders). Results are merged.
        /// </summary>
        System.Threading.Tasks.Task<List<Reminder>> GetCalendarRemindersAsync(string? managerCode, string? ownerId, DateOnly startDate, DateOnly endDate);

        /// <summary>
        /// Creates a new reminder for the specified user.
        /// </summary>
        System.Threading.Tasks.Task<Reminder> CreateReminderAsync(Reminder reminder);

        /// <summary>
        /// Deletes a reminder by ID (only if it belongs to the specified user).
        /// Returns true if the reminder was deleted; false if not found or not owned by user.
        /// </summary>
        System.Threading.Tasks.Task<bool> DeleteReminderAsync(string id, string username);

        /// <summary>
        /// Updates an existing reminder (only if it belongs to the specified user).
        /// Returns the updated reminder if successful; null if not found or not owned by user.
        /// </summary>
        System.Threading.Tasks.Task<Reminder?> UpdateReminderAsync(string id, string username, string title, string? notes, DateTime reminderDateTimeUtc, string? color, string? ownerId, string? forUsername, string? forDisplayName, string? managerCode);
    }
}
