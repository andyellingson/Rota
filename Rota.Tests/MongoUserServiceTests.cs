using System;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using MongoDB.Driver;
using NUnit.Framework;
using Rota.Services;

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
            var findFluentMock = new Mock<IFindFluent<User, User>>();

            var expected = new User { Username = "bob" };

            // Setup the fluent find to return the expected user
            findFluentMock.Setup(f => f.FirstOrDefaultAsync(It.IsAny<CancellationToken>())).ReturnsAsync(expected);

            // Setup collection.Find(expression, ...) to return our fluent mock
            collectionMock
                .Setup(c => c.Find(It.IsAny<Expression<Func<User, bool>>>(), It.IsAny<FindOptions>()))
                .Returns(findFluentMock.Object);

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
            var findFluentMock = new Mock<IFindFluent<User, User>>();

            var hashed = BCrypt.Net.BCrypt.HashPassword("secret");
            var expected = new User { Username = "alice", PasswordHash = hashed };

            findFluentMock.Setup(f => f.FirstOrDefaultAsync(It.IsAny<CancellationToken>())).ReturnsAsync(expected);
            collectionMock
                .Setup(c => c.Find(It.IsAny<Expression<Func<User, bool>>>(), It.IsAny<FindOptions>()))
                .Returns(findFluentMock.Object);

            var loggerMock = new Mock<ILogger<MongoUserService>>();
            var service = new MongoUserService(collectionMock.Object, loggerMock.Object);

            var ok = await service.ValidateCredentialsAsync("alice", "secret");

            Assert.IsTrue(ok);
        }

        [Test]
        public async Task ValidateCredentialsAsync_ReturnsFalse_WhenUserNotFound()
        {
            var collectionMock = new Mock<IMongoCollection<User>>();
            var findFluentMock = new Mock<IFindFluent<User, User>>();

            findFluentMock.Setup(f => f.FirstOrDefaultAsync(It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);
            collectionMock
                .Setup(c => c.Find(It.IsAny<Expression<Func<User, bool>>>(), It.IsAny<FindOptions>()))
                .Returns(findFluentMock.Object);

            var loggerMock = new Mock<ILogger<MongoUserService>>();
            var service = new MongoUserService(collectionMock.Object, loggerMock.Object);

            var ok = await service.ValidateCredentialsAsync("nobody", "x");

            Assert.IsFalse(ok);
        }
    }
}
