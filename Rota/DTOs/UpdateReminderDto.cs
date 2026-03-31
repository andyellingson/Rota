namespace Rota.DTOs
{
    /// <summary>
    /// DTO for updating an existing reminder.
    /// </summary>
    /// <param name="Title">Updated title/description of the reminder.</param>
    /// <param name="Notes">Updated optional detailed notes.</param>
    /// <param name="Date">Updated date for the reminder (ISO 8601 format).</param>
    /// <param name="Color">Optional CSS color string (e.g. "#ffeb3b").</param>
    /// <param name="ForUsername">Username of the worker or manager the reminder is for.</param>
    public record UpdateReminderDto(string Title, string? Notes, string Date, string? Color, string? ForUsername = null);
}
