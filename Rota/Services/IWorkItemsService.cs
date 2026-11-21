using Rota.Models;

namespace Rota.Services
{
    /// <summary>
    /// Represents the result of a cover-request action for a work item.
    /// </summary>
    public sealed record WorkItemCoverActionResult(bool Success, string? ErrorMessage, WorkItem? WorkItem);

    /// <summary>
    /// Service contract for managing work items and work item templates.
    /// </summary>
    public interface IWorkItemsService
    {
        /// <summary>
        /// Returns work items matching the supplied user scope and date window.
        /// </summary>
        System.Threading.Tasks.Task<List<WorkItem>> GetWorkItemsAsync(string? username, string? userId, string? teamCode, DateOnly startDate, DateOnly endDate);

        /// <summary>
        /// Creates a new work item.
        /// </summary>
        System.Threading.Tasks.Task<WorkItem> CreateWorkItemAsync(WorkItem WorkItem);

        /// <summary>
        /// Creates multiple work items in a single operation.
        /// </summary>
        System.Threading.Tasks.Task<List<WorkItem>> CreateWorkItemsAsync(IEnumerable<WorkItem> workItems);

        /// <summary>
        /// Deletes a work item owned by the specified username.
        /// </summary>
        System.Threading.Tasks.Task<bool> DeleteWorkItemAsync(string id, string username);

        /// <summary>
        /// Deletes multiple work items owned by the specified username.
        /// </summary>
        System.Threading.Tasks.Task<int> DeleteWorkItemsAsync(IEnumerable<string> ids, string username);

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
        System.Threading.Tasks.Task<WorkItem?> UpdateWorkItemAsync(string id, string username, DateTime startUtc, DateTime endUtc, string? title, string? notes, string workerType, string? color, string? assignedToUserId, bool autoAssignedFromAdditionalQualifiedType = false);

        /// <summary>
        /// Returns the distinct ScheduleIds of every work item the given user is assigned to.
        /// </summary>
        System.Threading.Tasks.Task<List<string>> GetDistinctScheduleIdsForUserAsync(string userId);

        /// <summary>
        /// Marks a future work item as requesting cover for the current assignee.
        /// </summary>
        System.Threading.Tasks.Task<WorkItemCoverActionResult> RequestCoverAsync(string workItemId, string requesterUserId, string? teamCode);

        /// <summary>
        /// Reassigns a cover-requested future work item to an eligible teammate.
        /// </summary>
        System.Threading.Tasks.Task<WorkItemCoverActionResult> PickupCoverAsync(string workItemId, string pickerUserId, string? teamCode);

        /// <summary>
        /// Clears the cover request on a future work item for the currently assigned team member.
        /// </summary>
        System.Threading.Tasks.Task<WorkItemCoverActionResult> CancelCoverAsync(string workItemId, string requesterUserId, string? teamCode);

        /// <summary>
        /// Gets all template work items for a specific Work Item Template ID.
        /// These are work items with RotationId set and are not tied to specific calendar dates.
        /// </summary>
        System.Threading.Tasks.Task<List<WorkItem>> GetWorkItemTemplateWorkItemsAsync(string workItemTemplateId, string teamCode);

        /// <summary>
        /// Deletes all template work items associated with a specific Work Item Template ID.
        /// </summary>
        System.Threading.Tasks.Task<int> DeleteWorkItemTemplateWorkItemsAsync(string workItemTemplateId, string teamCode);
    }
}
