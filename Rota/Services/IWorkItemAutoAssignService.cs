using Rota.Models;

namespace Rota.Services
{
    /// <summary>
    /// Outcome of an auto-assign run.
    /// </summary>
    /// <param name="WorkItemsChanged">True when at least one work item was assigned (the calendar should reload).</param>
    /// <param name="SuccessMessage">Informational message describing the result.</param>
    /// <param name="WarningMessage">Optional warning (for example employees without a weekly hour cap).</param>
    /// <param name="LinkedUsers">The freshly loaded linked users used for the run.</param>
    public sealed record AutoAssignOutcome(bool WorkItemsChanged, string? SuccessMessage, string? WarningMessage, List<User> LinkedUsers);

    /// <summary>
    /// Assigns eligible employees to unassigned work items.
    /// </summary>
    public interface IWorkItemAutoAssignService
    {
        /// <summary>
        /// Assigns employees to unassigned work items between <paramref name="rangeStart"/> and <paramref name="rangeEnd"/>,
        /// respecting team member type, priority, one-work-item-per-day and weekly MaxHours rules.
        /// </summary>
        /// <param name="username">Current user's username.</param>
        /// <param name="userId">Current user's id used when loading scoped work items.</param>
        /// <param name="managerCode">Manager code scoping work items and linked users.</param>
        /// <param name="rangeStart">Inclusive first local day to consider.</param>
        /// <param name="rangeEnd">Inclusive last local day to consider.</param>
        /// <param name="includeDay">Optional extra filter; when null every day in the range is considered.</param>
        /// <param name="noWorkItemsMessage">Message returned when there is nothing to assign.</param>
        Task<AutoAssignOutcome> AutoAssignAsync(
            string username,
            string? userId,
            string managerCode,
            DateOnly rangeStart,
            DateOnly rangeEnd,
            Func<DateOnly, bool>? includeDay,
            string noWorkItemsMessage);
    }
}
