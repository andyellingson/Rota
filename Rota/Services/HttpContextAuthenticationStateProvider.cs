using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging;

namespace Rota.Services
{
    /// <summary>
    /// An <see cref="AuthenticationStateProvider"/> that derives the current
    /// <see cref="AuthenticationState"/> from the ASP.NET Core <see cref="Microsoft.AspNetCore.Http.HttpContext"/>.
    /// This is useful for server-side scenarios where authentication is handled by the standard middleware (cookies).
    /// </summary>
    public class HttpContextAuthenticationStateProvider : AuthenticationStateProvider
    {
        private readonly Microsoft.AspNetCore.Http.IHttpContextAccessor _accessor;
        private readonly ILogger<HttpContextAuthenticationStateProvider> _logger;

        /// <summary>
        /// Constructs the provider with the required <see cref="IHttpContextAccessor"/>.
        /// An <see cref="ILogger{T}"/> is also injected for diagnostics.
        /// </summary>
        public HttpContextAuthenticationStateProvider(Microsoft.AspNetCore.Http.IHttpContextAccessor accessor, ILogger<HttpContextAuthenticationStateProvider> logger)
        {
            _accessor = accessor;
            _logger = logger;
        }

        /// <summary>
        /// Returns the current authentication state based on <see cref="HttpContext.User"/>.
        /// If there is no <see cref="HttpContext"/> or an error occurs, an anonymous user is returned.
        /// </summary>
        public override System.Threading.Tasks.Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            try
            {
                // If HttpContext is available, use its User principal. Otherwise return an empty identity.
                var user = _accessor.HttpContext?.User ?? new ClaimsPrincipal(new ClaimsIdentity());
                return System.Threading.Tasks.Task.FromResult(new AuthenticationState(user));
            }
            catch (System.Exception ex)
            {
                // Log and fall back to an anonymous user so Blazor continues functioning even if something goes wrong.
                _logger.LogError(ex, "Error getting AuthenticationState from HttpContext");
                var anon = new ClaimsPrincipal(new ClaimsIdentity());
                return System.Threading.Tasks.Task.FromResult(new AuthenticationState(anon));
            }
        }
    }
}
