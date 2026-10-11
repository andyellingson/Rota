using System;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using MongoDB.Driver;
using NUnit.Framework;
using Rota.Services;
using Rota.Models;

namespace Rota.Tests
{
    public class MongoUserServiceTests
    {
        [Test]
        public async Task CreateUserAsync_CallsInsertOne()
        {
            var collectionMock = new Mock<IMongoCollection<User>>();
            // Setup InsertOneAsync to complete
            collectionMock
                .Setup(c => c.InsertOneAsync(It.IsAny<User>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask)
                .Verifiable();

            var loggerMock = new Mock<ILogger<MongoUserService>>();

            // Create service using the test constructor that accepts a collection
            var service = new MongoUserService(collectionMock.Object, loggerMock.Object);

            var user = new User { Username = "u1" };
            await service.CreateUserAsync(user, "password");

            collectionMock.Verify();
            Assert.IsFalse(string.IsNullOrEmpty(user.PasswordHash));
            Assert.IsTrue(BCrypt.Net.BCrypt.Verify("password", user.PasswordHash));
        }

        [Test]
        public async Task GetByUsernameAsync_ReturnsUser_WhenFound()
        {
            var collectionMock = new Mock<IMongoCollection<User>>();
            var expected = new User { Username = "bob" };
            SetupFind(collectionMock, expected);

            var loggerMock = new Mock<ILogger<MongoUserService>>();
            var service = new MongoUserService(collectionMock.Object, loggerMock.Object);

            var actual = await service.GetByUsernameAsync("bob");

            Assert.IsNotNull(actual);
            Assert.AreEqual(expected.Username, actual!.Username);
        }

        [Test]
        public async Task ValidateCredentialsAsync_ReturnsTrue_WhenPasswordMatches()
        {
            var collectionMock = new Mock<IMongoCollection<User>>();
            var hashed = BCrypt.Net.BCrypt.HashPassword("secret");
            var expected = new User { Username = "alice", PasswordHash = hashed };
            SetupFind(collectionMock, expected);

            var loggerMock = new Mock<ILogger<MongoUserService>>();
            var service = new MongoUserService(collectionMock.Object, loggerMock.Object);

            var ok = await service.ValidateCredentialsAsync("alice", "secret");

            Assert.IsTrue(ok);
        }

        [Test]
        public async Task ValidateCredentialsAsync_ReturnsFalse_WhenUserNotFound()
        {
            var collectionMock = new Mock<IMongoCollection<User>>();
            SetupFind(collectionMock);

            var loggerMock = new Mock<ILogger<MongoUserService>>();
            var service = new MongoUserService(collectionMock.Object, loggerMock.Object);

            var ok = await service.ValidateCredentialsAsync("nobody", "x");

            Assert.IsFalse(ok);
        }

        [Test]
        public async Task UserProfileAndPreferenceUpdates_ReturnSuccessWhenMongoReportsModified()
        {
            var collection = new Mock<IMongoCollection<User>>();
            var updateResult = new Mock<UpdateResult>();
            updateResult.SetupGet(r => r.ModifiedCount).Returns(1);
            updateResult.SetupGet(r => r.MatchedCount).Returns(1);
            collection.Setup(c => c.UpdateOneAsync(It.IsAny<FilterDefinition<User>>(), It.IsAny<UpdateDefinition<User>>(), It.IsAny<UpdateOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(updateResult.Object);
            var service = new MongoUserService(collection.Object, Mock.Of<ILogger<MongoUserService>>());
            var availability = new List<UserAvailability>();

            Assert.Multiple(async () =>
            {
                Assert.That(await service.UpdateNamesAsync("user", "First", "Last"), Is.True);
                Assert.That(await service.UpdateMaxHoursAsync("user", 32), Is.True);
                Assert.That(await service.UpdateShiftAssignmentPriorityAsync("user", 4), Is.True);
                Assert.That(await service.UpdateAssignedSchedulesAsync("user", ["schedule-a", "schedule-a", "schedule-b"]), Is.True);
                Assert.That(await service.UpdateDisplayNameAsync("user", "Name"), Is.True);
                Assert.That(await service.UpdateOccupationAsync("user", "Cook"), Is.True);
                Assert.That(await service.UpdateAvailabilityAsync("user", availability), Is.True);
                Assert.That(await service.UpdateThemeAsync("user", "amoled"), Is.True);
            });

            collection.Verify(c => c.UpdateOneAsync(It.IsAny<FilterDefinition<User>>(), It.IsAny<UpdateDefinition<User>>(), It.IsAny<UpdateOptions>(), It.IsAny<CancellationToken>()), Times.Exactly(8));
        }

        [Test]
        public async Task UpdatePasswordAsync_RejectsIncorrectCurrentPassword()
        {
            var collection = new Mock<IMongoCollection<User>>();
            SetupFind(collection, new User { Username = "user", PasswordHash = BCrypt.Net.BCrypt.HashPassword("current") });
            var service = new MongoUserService(collection.Object, Mock.Of<ILogger<MongoUserService>>());

            var result = await service.UpdatePasswordAsync("user", "wrong", "new");

            Assert.That(result, Is.False);
            Assert.That(collection.Invocations.Any(invocation => invocation.Method.Name == "UpdateOneAsync"), Is.False);
        }

        [Test]
        public async Task UpdatePasswordAsync_ReplacesHashWhenCurrentPasswordMatches()
        {
            var collection = new Mock<IMongoCollection<User>>();
            var user = new User { Username = "user", PasswordHash = BCrypt.Net.BCrypt.HashPassword("current") };
            SetupFind(collection, user);
            collection.Setup(c => c.UpdateOneAsync(It.IsAny<FilterDefinition<User>>(), It.IsAny<UpdateDefinition<User>>(), It.IsAny<UpdateOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Mock.Of<UpdateResult>());
            var service = new MongoUserService(collection.Object, Mock.Of<ILogger<MongoUserService>>());

            var result = await service.UpdatePasswordAsync("user", "current", "new");

            Assert.That(result, Is.True);
            collection.Verify(c => c.UpdateOneAsync(It.IsAny<FilterDefinition<User>>(), It.IsAny<UpdateDefinition<User>>(), It.IsAny<UpdateOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public async Task UpdateDisplayNameAsync_ReturnsFalseWhenMongoThrows()
        {
            var collection = new Mock<IMongoCollection<User>>();
            collection.Setup(c => c.UpdateOneAsync(It.IsAny<FilterDefinition<User>>(), It.IsAny<UpdateDefinition<User>>(), It.IsAny<UpdateOptions>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException());
            var service = new MongoUserService(collection.Object, Mock.Of<ILogger<MongoUserService>>());

            Assert.That(await service.UpdateDisplayNameAsync("user", "Name"), Is.False);
        }

        private static void SetupFind(Mock<IMongoCollection<User>> collection, params User[] users)
        {
            var cursor = new Mock<IAsyncCursor<User>>();
            cursor.SetupSequence(c => c.MoveNext(It.IsAny<CancellationToken>())).Returns(users.Length > 0).Returns(false);
            cursor.SetupSequence(c => c.MoveNextAsync(It.IsAny<CancellationToken>())).ReturnsAsync(users.Length > 0).ReturnsAsync(false);
            cursor.SetupGet(c => c.Current).Returns(users);
            collection.Setup(c => c.FindAsync(It.IsAny<FilterDefinition<User>>(), It.IsAny<FindOptions<User, User>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(cursor.Object);
            collection.Setup(c => c.FindSync(It.IsAny<IClientSessionHandle>(), It.IsAny<FilterDefinition<User>>(), It.IsAny<FindOptions<User, User>>(), It.IsAny<CancellationToken>()))
                .Returns(cursor.Object);
        }
    }
}
