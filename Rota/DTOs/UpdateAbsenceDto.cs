namespace Rota.DTOs
{
    /// <summary>DTO for updating an existing absence entry.</summary>
    /// <param name="StartDate">Start date ISO 8601 string (date or date-time). The server will interpret the date in UTC unless a time is supplied separately.</param>
    /// <param name="DayCount">Number of consecutive absent days (1–365). If >1 any supplied times are ignored - times only apply to single-day absences.</param>
    /// <param name="Title">Short description, e.g. "Sick Leave".</param>
    /// <param name="Notes">Optional additional details.</param>
    /// <param name="Color">Optional CSS color string.</param>
    /// <param name="AssignedToUserId">MongoDB ObjectId string of the user the absence is for.</param>
    /// <param name="StartTime">Optional local time string (e.g. "09:00") for part-day absences. Only used when DayCount == 1.</param>
    /// <param name="EndTime">Optional local time string (e.g. "17:00") for part-day absences. Only used when DayCount == 1.</param>
    public record UpdateAbsenceDto(string StartDate, int DayCount, string Title, string? Notes, string? Color, string? AssignedToUserId = null, string? StartTime = null, string? EndTime = null);
}
