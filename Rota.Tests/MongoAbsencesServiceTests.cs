using System.Threading;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using Moq;
using NUnit.Framework;
using Rota.Models;
using Rota.Services;

namespace Rota.Tests
{
    public class MongoAbsencesServiceTests
    {
        [Test]
        public async Task DeleteAbsenceAsync_WithoutManagerCode_FiltersByCreatorOnly()
        {
            var collection = new Mock<IMongoCollection<Absence>>();
            FilterDefinition<Absence>? capturedFilter = null;
            collection
                .Setup(c => c.DeleteOneAsync(It.IsAny<FilterDefinition<Absence>>(), It.IsAny<CancellationToken>()))
                .Callback<FilterDefinition<Absence>, CancellationToken>((filter, _) => capturedFilter = filter)
                .ReturnsAsync(new TestDeleteResult());

            var service = CreateService(collection);
            await service.DeleteAbsenceAsync("507f1f77bcf86cd799439011", "creator");

            var rendered = Render(capturedFilter);
            Assert.That(rendered.ToJson(), Does.Contain("creator"));
            Assert.That(rendered.ToJson(), Does.Not.Contain("managerCode"));
        }

        [Test]
        public async Task DeleteAbsenceAsync_WithManagerCode_FiltersByCreatorOrManagerScope()
        {
            var collection = new Mock<IMongoCollection<Absence>>();
            FilterDefinition<Absence>? capturedFilter = null;
            collection
                .Setup(c => c.DeleteOneAsync(It.IsAny<FilterDefinition<Absence>>(), It.IsAny<CancellationToken>()))
                .Callback<FilterDefinition<Absence>, CancellationToken>((filter, _) => capturedFilter = filter)
                .ReturnsAsync(new TestDeleteResult());

            var service = CreateService(collection);
            await service.DeleteAbsenceAsync("507f1f77bcf86cd799439011", "manager", "linked-manager-code");

            var rendered = Render(capturedFilter).ToJson();
            Assert.That(rendered, Does.Contain("manager"));
            Assert.That(rendered, Does.Contain("linked-manager-code"));
            Assert.That(rendered, Does.Contain("managerCode"));
        }

        [Test]
        public async Task UpdateAbsenceAsync_WithoutManagerCode_FiltersByCreatorOnly()
        {
            var collection = new Mock<IMongoCollection<Absence>>();
            FilterDefinition<Absence>? capturedFilter = null;
            collection
                .Setup(c => c.FindOneAndUpdateAsync(
                    It.IsAny<FilterDefinition<Absence>>(),
                    It.IsAny<UpdateDefinition<Absence>>(),
                    It.IsAny<FindOneAndUpdateOptions<Absence, Absence>>(),
                    It.IsAny<CancellationToken>()))
                .Callback<FilterDefinition<Absence>, UpdateDefinition<Absence>, FindOneAndUpdateOptions<Absence, Absence>, CancellationToken>((filter, _, _, _) => capturedFilter = filter)
                .ReturnsAsync((Absence?)null);

            var service = CreateService(collection);
            await service.UpdateAbsenceAsync(
                "507f1f77bcf86cd799439011", "creator", "title", null,
                DateTime.UtcNow, DateTime.UtcNow.AddDays(1), 1, null,
                null, null, null);

            var rendered = Render(capturedFilter).ToJson();
            Assert.That(rendered, Does.Contain("creator"));
            Assert.That(rendered, Does.Not.Contain("managerCode"));
        }

        private sealed class TestDeleteResult : DeleteResult
        {
            public override long DeletedCount => 0;
            public override bool IsAcknowledged => true;
        }

        private static MongoAbsencesService CreateService(Mock<IMongoCollection<Absence>> collection)
        {
            return new MongoAbsencesService(collection.Object, Mock.Of<ILogger<MongoAbsencesService>>());
        }

        private static BsonDocument Render(FilterDefinition<Absence>? filter)
        {
            Assert.That(filter, Is.Not.Null);
            var registry = BsonSerializer.SerializerRegistry;
            var serializer = registry.GetSerializer<Absence>();
            return filter!.Render(new RenderArgs<Absence>(serializer, registry));
        }
    }
}