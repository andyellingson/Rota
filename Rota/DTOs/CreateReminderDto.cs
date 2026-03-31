namespace Rota.DTOs
{
    /// <summary>
    /// DTO for creating a new reminder.
    /// </summary>
    /// <param name="Date">The date for the reminder (ISO 8601 format).</param>
    /// <param name="Title">Title/description of the reminder.</param>
    /// <param name="Notes">Optional detailed notes.</param>
    /// <param name="Color">Optional CSS color string (e.g. "#ffeb3b").</param>
    /// <param name="ForUsername">Username of the worker or manager the reminder is for.</param>
    public record CreateReminderDto(string Date, string Title, string? Notes, string? Color, string? ForUsername = null);
}
