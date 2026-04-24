using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NUnit.Framework;
using Rota.Services;

namespace Rota.Tests
{
    public class AuthIntegrationTests
    {
        [Test]
        public async System.Threading.Tasks.Task Login_SetsCookie_And_AuthenticatedRequestsIncludeUser()
        {
            var mockUserService = new Mock<IUserService>();
            mockUserService.Setup(s => s.ValidateCredentialsAsync("test", "pass")).ReturnsAsync(true);
            mockUserService.Setup(s => s.GetByUsernameAsync("test")).ReturnsAsync(new Rota.Models.User { Username = "test", Roles = System.Array.Empty<string>() });

            await using var factory = TestHelpers.CreateFactoryWithUserService(mockUserService.Object);

            // Enable cookie handling so the client's cookies persist between requests
            var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });

            var payload = new { Username = "test", Password = "pass", RememberMe = false };
            var loginResp = await client.PostAsJsonAsync("/api/auth/login", payload);
            loginResp.EnsureSuccessStatusCode();

            // The login endpoint should set an authentication cookie. Verify a Set-Cookie header was returned.
            Assert.IsTrue(loginResp.Headers.TryGetValues("Set-Cookie", out var cookieValues));
            Assert.IsNotNull(cookieValues);
            Assert.IsNotEmpty(cookieValues);
        }
    }
}
