namespace Rota.DTOs
{
    /// <summary>DTO for creating an absence entry.</summary>
    /// <param name="StartDate">Start date ISO 8601 string.</param>
    /// <param name="DayCount">Number of consecutive absent days (1–365).</param>
    /// <param name="Title">Short description, e.g. "Sick Leave".</param>
    /// <param name="Notes">Optional additional details.</param>
    /// <param name="Color">Optional CSS color string.</param>
    /// <param name="ForUsername">Username of the worker or manager the absence is for.</param>
    public record CreateAbsenceDto(string StartDate, int DayCount, string Title, string? Notes, string? Color, string? ForUsername = null);
}
