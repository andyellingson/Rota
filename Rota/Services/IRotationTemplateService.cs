namespace Rota.Services
{
    /// <summary>
    /// Outcome of applying a rotation template to a set of days.
    /// </summary>
    /// <param name="CreatedCount">Number of shifts created.</param>
    /// <param name="Error">User-facing validation error, or null when the template was applied.</param>
    public sealed record RotationApplyResult(int CreatedCount, string? Error = null);

    /// <summary>
    /// Creates calendar shifts from the template shifts of a rotation.
    /// </summary>
    public interface IRotationTemplateService
    {
        /// <summary>
        /// Creates one shift per matching template shift for each of the supplied days
        /// (a template matches a day when its WorkDay equals the day's DayOfWeek).
        /// </summary>
        /// <param name="rotationId">Identifier of the rotation whose template shifts are applied.</param>
        /// <param name="username">Username recorded as the creator of the shifts.</param>
        /// <param name="managerCode">Manager code that owns the rotation and shifts.</param>
        /// <param name="scheduleId">Schedule the created shifts belong to.</param>
        /// <param name="days">Days to apply the template to.</param>
        Task<RotationApplyResult> ApplyAsync(string rotationId, string username, string managerCode, string scheduleId, IEnumerable<DateOnly> days);
    }
}
