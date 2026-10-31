using Rota.Models;

namespace Rota.Services
{
    /// <summary>
    /// Service contract for managing work items and work item templates.
    /// </summary>
    public interface IWorkItemsService
    {
        /// <summary>
        /// Returns work items matching the supplied user scope and date window.
        /// </summary>
        System.Threading.Tasks.Task<List<WorkItem>> GetWorkItemsAsync(string? username, string? userId, string? managerCode, DateOnly startDate, DateOnly endDate);

        /// <summary>
        /// Creates a new work item.
        /// </summary>
        System.Threading.Tasks.Task<WorkItem> CreateWorkItemAsync(WorkItem WorkItem);

        /// <summary>
        /// Deletes a work item owned by the specified username.
        /// </summary>
        System.Threading.Tasks.Task<bool> DeleteWorkItemAsync(string id, string username);

        /// <summary>
        /// Deletes every work item belonging to the specified schedule.
        /// </summary>
        System.Threading.Tasks.Task<long> DeleteWorkItemsByScheduleIdAsync(string scheduleId);

        /// <summary>
        /// Deletes all work items in a recurring series for the specified user.
        /// </summary>
        System.Threading.Tasks.Task<int> DeleteWorkItemsBySeriesIdAsync(Guid seriesId, string username);

        /// <summary>
        /// Updates an existing work item owned by the specified username.
        /// </summary>
        System.Threading.Tasks.Task<WorkItem?> UpdateWorkItemAsync(string id, string username, DateTime startUtc, DateTime endUtc, string? title, string? notes, string workerType, string? color, string? assignedToUserId);

        /// <summary>
        /// Returns the distinct ScheduleIds of every work item the given user is assigned to.
        /// </summary>
        System.Threading.Tasks.Task<List<string>> GetDistinctScheduleIdsForUserAsync(string userId);

        /// <summary>
        /// Gets all template work items for a specific Rotation ID.
        /// These are work items with RotationId set and are not tied to specific calendar dates.
        /// </summary>
        System.Threading.Tasks.Task<List<WorkItem>> GetRotationTemplateWorkItemsAsync(string rotationId, string managerCode);

        /// <summary>
        /// Deletes all template work items associated with a specific Rotation ID.
        /// </summary>
        System.Threading.Tasks.Task<int> DeleteRotationTemplateWorkItemsAsync(string rotationId, string managerCode);
    }
}
