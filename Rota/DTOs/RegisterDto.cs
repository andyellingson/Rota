namespace Rota.DTOs
{
    /// <summary>
    /// DTO used for user registration requests.
    /// Distinct from <see cref="LoginDto"/> to allow future divergence of fields.
    /// </summary>
    /// <param name="Username">Desired username.</param>
    /// <param name="Password">Plain-text password.</param>
    /// <param name="RememberMe">Whether to issue a persistent cookie after registration.</param>
    /// <param name="DisplayName">Optional human-friendly display name.</param>
    /// <param name="Role">Role to assign to the new account.</param>
    /// <param name="LinkManagerCode">Optional manager code for employees to link at registration.</param>
    public record RegisterDto(string Username, string Password, bool RememberMe, string? DisplayName, string? Role, string? LinkManagerCode);
}
