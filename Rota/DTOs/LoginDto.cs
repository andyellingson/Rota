namespace Rota.DTOs
{
    /// <summary>
    /// DTO used by the authentication endpoints for login requests.
    /// </summary>
    /// <param name="Username">The username provided by the client.</param>
    /// <param name="Password">The plain-text password provided by the client.</param>
    /// <param name="RememberMe">Whether to issue a persistent authentication cookie.</param>
    public record LoginDto(string Username, string Password, bool RememberMe);
}
