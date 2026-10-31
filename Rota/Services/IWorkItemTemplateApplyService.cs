namespace Rota.Services
{
    /// <summary>
    /// Outcome of applying a Work Item Template to a set of days.
    /// </summary>
    /// <param name="CreatedCount">Number of work items created.</param>
    /// <param name="Error">User-facing validation error, or null when the template was applied.</param>
    public sealed record WorkItemTemplateApplyResult(int CreatedCount, string? Error = null);

    /// <summary>
    /// Creates calendar work items from the template work items of a Work Item Template.
    /// </summary>
    public interface IWorkItemTemplateApplyService
    {
        /// <summary>
        /// Creates one work item per matching template work item for each of the supplied days
        /// (a template matches a day when its WorkDay equals the day's DayOfWeek).
        /// </summary>
        /// <param name="workItemTemplateId">Identifier of the Work Item Template whose template work items are applied.</param>
        /// <param name="username">Username recorded as the creator of the work items.</param>
        /// <param name="teamCode">Team code that owns the Work Item Template and work items.</param>
        /// <param name="scheduleId">Schedule the created work items belong to.</param>
        /// <param name="days">Days to apply the template to.</param>
        Task<WorkItemTemplateApplyResult> ApplyAsync(string workItemTemplateId, string username, string teamCode, string scheduleId, IEnumerable<DateOnly> days);
    }
}
