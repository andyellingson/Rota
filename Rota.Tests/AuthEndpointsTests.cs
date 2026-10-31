using System.Net.Http.Json;
using System.Text.Json;
using Moq;
using NUnit.Framework;
using Rota.Models;
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
            var mockSchedulesService = new Mock<ISchedulesService>();
            User? createdUser = null;

            // No existing user.
            mockUserService.Setup(s => s.GetByUsernameAsync("newuser")).ReturnsAsync((User?)null);
            mockUserService.Setup(s => s.CreateUserAsync(It.IsAny<User>(), It.IsAny<string>()))
                .Callback<User, string>((user, _) => createdUser = user)
                .Returns(System.Threading.Tasks.Task.CompletedTask);

            await using var factory = TestHelpers.CreateFactoryWithUserService(mockUserService.Object, mockSchedulesService.Object);
            var client = factory.CreateClient();

            var payload = new { Username = "newuser", Password = "p", RememberMe = false };
            var resp = await client.PostAsJsonAsync("/api/auth/register", payload);

            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadFromJsonAsync<JsonElement>();

            Assert.Multiple(() =>
            {
                Assert.IsTrue(json.GetProperty("ok").GetBoolean());
                Assert.AreEqual(200, json.GetProperty("code").GetInt32());
                Assert.That(createdUser, Is.Not.Null);
                Assert.That(createdUser!.MaxHours, Is.EqualTo(40));
                Assert.That(createdUser.Availability.Count, Is.EqualTo(7));
                Assert.That(createdUser.Availability.All(a => a.StartTime == "00:00" && a.EndTime == "23:59"), Is.True);
            });
        }

        [Test]
        public async System.Threading.Tasks.Task Register_LinksEmployeeAndAssignsAllManagerSchedules()
        {
            var mockUserService = new Mock<IUserService>();
            var mockSchedulesService = new Mock<ISchedulesService>();

            mockUserService.Setup(s => s.GetByUsernameAsync("employee1")).ReturnsAsync((User?)null);
            mockUserService.Setup(s => s.CreateUserAsync(It.IsAny<User>(), It.IsAny<string>())).Returns(System.Threading.Tasks.Task.CompletedTask);
            mockUserService.Setup(s => s.LinkToManagerAsync("employee1", "MGR-123")).ReturnsAsync(true);
            mockUserService.Setup(s => s.GetByManagerCodeAsync("MGR-123")).ReturnsAsync(new User
            {
                Id = "manager-id",
                Username = "manager",
                TeamCode = "MGR-123",
                Roles = [Roles.Manager]
            });

            mockSchedulesService
                .Setup(s => s.GetSchedulesForManagerAsync("manager-id", "MGR-123"))
                .ReturnsAsync([
                    new Schedule { Id = "schedule-1", Name = "Main", ManagerId = "manager-id", ManagerUsername = "manager" },
                    new Schedule { Id = "schedule-2", Name = "Backup", ManagerId = "manager-id", ManagerUsername = "manager" }
                ]);

            await using var factory = TestHelpers.CreateFactoryWithUserService(mockUserService.Object, mockSchedulesService.Object);
            var client = factory.CreateClient();

            var payload = new { Username = "employee1", Password = "p", RememberMe = false, Role = Roles.Employee, LinkManagerCode = "MGR-123" };
            var resp = await client.PostAsJsonAsync("/api/auth/register", payload);

            resp.EnsureSuccessStatusCode();
            mockUserService.Verify(s => s.UpdateAssignedSchedulesAsync("employee1", It.Is<IEnumerable<string>>(ids => ids.SequenceEqual(new[] { "schedule-1", "schedule-2" }))), Times.Once);
        }

        [Test]
        public async System.Threading.Tasks.Task Register_ReturnsConflict_WhenUserExists()
        {
            var mockUserService = new Mock<IUserService>();
            var mockSchedulesService = new Mock<ISchedulesService>();
            mockUserService.Setup(s => s.GetByUsernameAsync("exists")).ReturnsAsync(new User { Username = "exists" });

            await using var factory = TestHelpers.CreateFactoryWithUserService(mockUserService.Object, mockSchedulesService.Object);
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
