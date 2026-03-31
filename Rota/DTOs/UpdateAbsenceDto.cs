namespace Rota.DTOs
{
    /// <summary>DTO for updating an existing absence entry.</summary>
    public record UpdateAbsenceDto(string StartDate, int DayCount, string Title, string? Notes, string? Color, string? ForUsername = null);
}
