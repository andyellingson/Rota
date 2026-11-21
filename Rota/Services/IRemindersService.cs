using Rota.Models;

namespace Rota.Services
{
    /// <summary>
    /// Service interface for managing user reminders.
    /// </summary>
    public interface IRemindersService
    {
        /// <summary>
        /// Fetches public team reminders and reminders visible to the requesting user.
        /// </summary>
        System.Threading.Tasks.Task<List<Reminder>> GetCalendarRemindersAsync(string? teamCode, string? ownerId, DateOnly startDate, DateOnly endDate, bool isManager = false);

        /// <summary>
        /// Creates a new reminder for the specified user.
        /// </summary>
        System.Threading.Tasks.Task<Reminder> CreateReminderAsync(Reminder reminder);

        /// <summary>
        /// Deletes a reminder by ID if the requesting user is its creator, recipient, or team manager.
        /// </summary>
        System.Threading.Tasks.Task<bool> DeleteReminderAsync(string id, string requesterId, bool isManager = false, string? teamCode = null);

        /// <summary>
        /// Deletes every reminder belonging to the given schedule. Returns the number deleted.
        /// </summary>
        System.Threading.Tasks.Task<long> DeleteRemindersByScheduleIdAsync(string scheduleId);

        /// <summary>
        /// Updates an existing reminder
        /// Returns the updated reminder if successful; null if not found or access is denied.
        /// </summary>
        System.Threading.Tasks.Task<Reminder?> UpdateReminderAsync(string id, string requesterId, bool isManager, string? teamCode, string title, string? notes, DateTime reminderDateTimeUtc, string? color, string? ownerId, string? forUsername, string? forDisplayName, ReminderVisibility visibility);
    }
}
