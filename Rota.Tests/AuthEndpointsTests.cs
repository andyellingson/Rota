using System.Net.Http.Json;
using System.Text.Json;
using Moq;
using NUnit.Framework;
using Rota.Services;

namespace Rota.Tests
{
    public class AuthEndpointsTests
    {
        [Test]
        public async System.Threading.Tasks.Task Login_ReturnsOk_WhenCredentialsValid()
        {
            var mockUserService = new Mock<IUserService>();
            mockUserService.Setup(s => s.ValidateCredentialsAsync("test", "pass")).ReturnsAsync(true);
            mockUserService.Setup(s => s.GetByUsernameAsync("test")).ReturnsAsync(new User { Username = "test", Roles = System.Array.Empty<string>() });

            await using var factory = TestHelpers.CreateFactoryWithUserService(mockUserService.Object);
            var client = factory.CreateClient();

            var payload = new { Username = "test", Password = "pass", RememberMe = false };
            var resp = await client.PostAsJsonAsync("/api/auth/login", payload);

            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadFromJsonAsync<JsonElement>();
            Assert.IsTrue(json.GetProperty("ok").GetBoolean());
            Assert.AreEqual(200, json.GetProperty("code").GetInt32());
        }

        [Test]
        public async System.Threading.Tasks.Task Register_ReturnsOk_WhenNewUser()
        {
            var mockUserService = new Mock<IUserService>();
            // No existing user
            mockUserService.Setup(s => s.GetByUsernameAsync("newuser")).ReturnsAsync((User?)null);
            mockUserService.Setup(s => s.CreateUserAsync(It.IsAny<User>(), It.IsAny<string>())).Returns(System.Threading.Tasks.Task.CompletedTask);

            await using var factory = TestHelpers.CreateFactoryWithUserService(mockUserService.Object);
            var client = factory.CreateClient();

            var payload = new { Username = "newuser", Password = "p", RememberMe = false };
            var resp = await client.PostAsJsonAsync("/api/auth/register", payload);

            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadFromJsonAsync<JsonElement>();
            Assert.IsTrue(json.GetProperty("ok").GetBoolean());
            Assert.AreEqual(200, json.GetProperty("code").GetInt32());
        }

        [Test]
        public async System.Threading.Tasks.Task Register_ReturnsConflict_WhenUserExists()
        {
            var mockUserService = new Mock<IUserService>();
            mockUserService.Setup(s => s.GetByUsernameAsync("exists")).ReturnsAsync(new User { Username = "exists" });

            await using var factory = TestHelpers.CreateFactoryWithUserService(mockUserService.Object);
            var client = factory.CreateClient();

            var payload = new { Username = "exists", Password = "p", RememberMe = false };
            var resp = await client.PostAsJsonAsync("/api/auth/register", payload);

            Assert.AreEqual(System.Net.HttpStatusCode.Conflict, resp.StatusCode);
            var json = await resp.Content.ReadFromJsonAsync<JsonElement>();
            Assert.IsFalse(json.GetProperty("ok").GetBoolean());
            Assert.AreEqual(409, json.GetProperty("code").GetInt32());
        }

        [Test]
        public async System.Threading.Tasks.Task Login_ReturnsUnauthorized_WhenCredentialsInvalid()
        {
            var mockUserService = new Mock<IUserService>();
            mockUserService.Setup(s => s.ValidateCredentialsAsync("bad", "creds")).ReturnsAsync(false);

            await using var factory = TestHelpers.CreateFactoryWithUserService(mockUserService.Object);
            var client = factory.CreateClient();

            var payload = new { Username = "bad", Password = "creds", RememberMe = false };
            var resp = await client.PostAsJsonAsync("/api/auth/login", payload);

            Assert.AreEqual(System.Net.HttpStatusCode.Unauthorized, resp.StatusCode);
            var json = await resp.Content.ReadFromJsonAsync<JsonElement>();
            Assert.IsFalse(json.GetProperty("ok").GetBoolean());
            Assert.AreEqual(401, json.GetProperty("code").GetInt32());
        }
    }
}
