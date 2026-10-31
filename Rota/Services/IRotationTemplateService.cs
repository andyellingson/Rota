namespace Rota.Services
{
    /// <summary>
    /// Outcome of applying a rotation template to a set of days.
    /// </summary>
    /// <param name="CreatedCount">Number of work items created.</param>
    /// <param name="Error">User-facing validation error, or null when the template was applied.</param>
    public sealed record RotationApplyResult(int CreatedCount, string? Error = null);

    /// <summary>
    /// Creates calendar work items from the template work items of a rotation.
    /// </summary>
    public interface IRotationTemplateService
    {
        /// <summary>
        /// Creates one work item per matching template work item for each of the supplied days
        /// (a template matches a day when its WorkDay equals the day's DayOfWeek).
        /// </summary>
        /// <param name="rotationId">Identifier of the rotation whose template work items are applied.</param>
        /// <param name="username">Username recorded as the creator of the work items.</param>
        /// <param name="managerCode">Manager code that owns the rotation and work items.</param>
        /// <param name="scheduleId">Schedule the created work items belong to.</param>
        /// <param name="days">Days to apply the template to.</param>
        Task<RotationApplyResult> ApplyAsync(string rotationId, string username, string managerCode, string scheduleId, IEnumerable<DateOnly> days);
    }
}
