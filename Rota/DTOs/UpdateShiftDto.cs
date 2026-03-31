namespace Rota.DTOs
{
    /// <summary>
    /// DTO for updating a shift.
    /// </summary>
    /// <param name="Start">Start date-time (ISO 8601)</param>
    /// <param name="End">End date-time (ISO 8601)</param>
    /// <param name="Title">Optional title</param>
    /// <param name="Notes">Optional notes</param>
    /// <param name="WorkerType">Worker type required (string)</param>
    /// <param name="Color">Optional CSS color string</param>
    /// <param name="AssignedToUsername">Username of the employee the shift is assigned to</param>
    public record UpdateShiftDto(string Start, string End, string? Title, string? Notes, string? WorkerType, string? Color, string? AssignedToUsername);
}
