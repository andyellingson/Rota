using System.Linq.Expressions;
using System.Threading;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using Moq;
using NUnit.Framework;
using Rota.Models;
using Rota.Services;

namespace Rota.Tests;

[TestFixture]
public class MongoShiftsServiceTests
{
    [Test]
    public async Task GetShiftsAsync_WithoutAnyScope_ReturnsEmptyWithoutQuery()
    {
        var collection = new Mock<IMongoCollection<Shift>>();
        var service = CreateService(collection);

        var result = await service.GetShiftsAsync(null, null, null, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));

        Assert.That(result, Is.Empty);
        Assert.That(collection.Invocations, Is.Empty);
    }

    [Test]
    public async Task GetShiftsAsync_WithManagerScopeUsesManagerFilterAndDateRange()
    {
        var collection = new Mock<IMongoCollection<Shift>>();
        FilterDefinition<Shift>? captured = null;
        var cursor = new Mock<IAsyncCursor<Shift>>();
        cursor.SetupSequence(c => c.MoveNext(It.IsAny<CancellationToken>())).Returns(true).Returns(false);
        cursor.SetupSequence(c => c.MoveNextAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true).ReturnsAsync(false);
        cursor.SetupGet(c => c.Current).Returns([]);
        collection.Setup(c => c.FindAsync(It.IsAny<FilterDefinition<Shift>>(), It.IsAny<FindOptions<Shift, Shift>>(), It.IsAny<CancellationToken>()))
            .Callback<FilterDefinition<Shift>, FindOptions<Shift, Shift>, CancellationToken>((filter, _, _) => captured = filter)
            .ReturnsAsync(cursor.Object);
        var service = CreateService(collection);

        await service.GetShiftsAsync("worker", "user-id", "manager-code", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));

        var json = Render(captured).ToJson();
        Assert.That(json, Does.Contain("manager-code"));
        Assert.That(json, Does.Contain("managerCode"));
        Assert.That(json, Does.Contain("start"));
        Assert.That(json, Does.Contain("end"));
    }

    [Test]
    public async Task CreateShiftAsync_SetsCreatedAtAndPersists()
    {
        var collection = new Mock<IMongoCollection<Shift>>();
        collection.Setup(c => c.InsertOneAsync(It.IsAny<Shift>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var service = CreateService(collection);
        var shift = new Shift { Username = "owner" };

        var result = await service.CreateShiftAsync(shift);

        Assert.That(result, Is.SameAs(shift));
        Assert.That(shift.CreatedAt, Is.EqualTo(DateTime.UtcNow).Within(TimeSpan.FromSeconds(2)));
        collection.Verify(c => c.InsertOneAsync(shift, It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task DeleteShiftAsync_ReturnsFalseWhenMongoFails()
    {
        var collection = new Mock<IMongoCollection<Shift>>();
        collection.Setup(c => c.DeleteOneAsync(It.IsAny<FilterDefinition<Shift>>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException());
        var service = CreateService(collection);

        Assert.That(await service.DeleteShiftAsync("id", "owner"), Is.False);
    }

    [Test]
    public async Task DeleteShiftsByScheduleIdAsync_DeletesMatchingScheduleShifts()
    {
        var collection = new Mock<IMongoCollection<Shift>>();
        FilterDefinition<Shift>? capturedFilter = null;
        var result = new Mock<DeleteResult>();
        result.SetupGet(r => r.DeletedCount).Returns(3);
        collection.Setup(c => c.DeleteManyAsync(It.IsAny<FilterDefinition<Shift>>(), It.IsAny<CancellationToken>()))
            .Callback<FilterDefinition<Shift>, CancellationToken>((filter, _) => capturedFilter = filter)
            .ReturnsAsync(result.Object);
        var service = CreateService(collection);

        var scheduleId = "507f1f77bcf86cd799439011";
        var deleted = await service.DeleteShiftsByScheduleIdAsync(scheduleId);

        Assert.That(deleted, Is.EqualTo(3));
        var filterJson = Render(capturedFilter).ToJson();
        Assert.That(filterJson, Does.Contain(scheduleId));
        Assert.That(filterJson, Does.Contain("scheduleId"));
    }

    [Test]
    public void DeleteShiftsByScheduleIdAsync_ThrowsWhenMongoFails()
    {
        var collection = new Mock<IMongoCollection<Shift>>();
        collection.Setup(c => c.DeleteManyAsync(It.IsAny<FilterDefinition<Shift>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException());
        var service = CreateService(collection);

        Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteShiftsByScheduleIdAsync("507f1f77bcf86cd799439011"));
    }

    [Test]
    public async Task DeleteShiftsBySeriesIdAsync_ReturnsZeroWhenMongoFails()
    {
        var collection = new Mock<IMongoCollection<Shift>>();
        collection.Setup(c => c.DeleteManyAsync(It.IsAny<FilterDefinition<Shift>>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException());
        var service = CreateService(collection);

        Assert.That(await service.DeleteShiftsBySeriesIdAsync(Guid.NewGuid(), "owner"), Is.Zero);
    }

    [Test]
    public async Task UpdateShiftAsync_ReturnsNullWhenMongoFails()
    {
        var collection = new Mock<IMongoCollection<Shift>>();
        collection.Setup(c => c.FindOneAndUpdateAsync(It.IsAny<FilterDefinition<Shift>>(), It.IsAny<UpdateDefinition<Shift>>(), It.IsAny<FindOneAndUpdateOptions<Shift, Shift>>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException());
        var service = CreateService(collection);

        var result = await service.UpdateShiftAsync("id", "owner", DateTime.UtcNow, DateTime.UtcNow.AddHours(1), "title", null, "Cook", null, null);

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task GetRotationTemplateShiftsAsync_ReturnsEmptyWhenMongoFails()
    {
        var collection = new Mock<IMongoCollection<Shift>>();
        collection.Setup(c => c.FindAsync(It.IsAny<FilterDefinition<Shift>>(), It.IsAny<FindOptions<Shift, Shift>>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException());
        var service = CreateService(collection);

        Assert.That(await service.GetRotationTemplateShiftsAsync("rotation", "manager"), Is.Empty);
    }

    [Test]
    public void DeleteRotationTemplateShiftsAsync_ThrowsWhenMongoFails()
    {
        var collection = new Mock<IMongoCollection<Shift>>();
        collection.Setup(c => c.DeleteManyAsync(It.IsAny<FilterDefinition<Shift>>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException());
        var service = CreateService(collection);

        Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteRotationTemplateShiftsAsync("rotation", "manager"));
    }

    private static MongoShiftsService CreateService(Mock<IMongoCollection<Shift>> collection) => new(collection.Object, Mock.Of<ILogger<MongoShiftsService>>());
    private static BsonDocument Render(FilterDefinition<Shift>? filter)
    {
        Assert.That(filter, Is.Not.Null);
        var registry = BsonSerializer.SerializerRegistry;
        return filter!.Render(new RenderArgs<Shift>(registry.GetSerializer<Shift>(), registry));
    }
}

[TestFixture]
public class MongoRemindersServiceTests
{
    [Test]
    public async Task GetCalendarRemindersAsync_WithoutOwnerOrManager_ReturnsEmptyWithoutQuery()
    {
        var collection = new Mock<IMongoCollection<Reminder>>();
        var service = new MongoRemindersService(collection.Object, Mock.Of<ILogger<MongoRemindersService>>());

        var result = await service.GetCalendarRemindersAsync(null, null, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));

        Assert.That(result, Is.Empty);
        Assert.That(collection.Invocations, Is.Empty);
    }

    [Test]
    public async Task CreateReminderAsync_SetsCreatedAtAndPersists()
    {
        var collection = new Mock<IMongoCollection<Reminder>>();
        collection.Setup(c => c.InsertOneAsync(It.IsAny<Reminder>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var service = new MongoRemindersService(collection.Object, Mock.Of<ILogger<MongoRemindersService>>());
        var reminder = new Reminder { Username = "owner" };

        await service.CreateReminderAsync(reminder);

        Assert.That(reminder.CreatedAt, Is.EqualTo(DateTime.UtcNow).Within(TimeSpan.FromSeconds(2)));
    }

    [Test]
    public async Task DeleteReminderAsync_ReturnsFalseOnDatabaseError()
    {
        var collection = new Mock<IMongoCollection<Reminder>>();
        collection.Setup(c => c.DeleteOneAsync(It.IsAny<FilterDefinition<Reminder>>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException());
        var service = new MongoRemindersService(collection.Object, Mock.Of<ILogger<MongoRemindersService>>());

        Assert.That(await service.DeleteReminderAsync("id", "owner"), Is.False);
    }

    [Test]
    public async Task UpdateReminderAsync_AppliesDefaultColorAndReturnsNullOnFailure()
    {
        var collection = new Mock<IMongoCollection<Reminder>>();
        UpdateDefinition<Reminder>? capturedUpdate = null;
        collection.Setup(c => c.FindOneAndUpdateAsync(It.IsAny<FilterDefinition<Reminder>>(), It.IsAny<UpdateDefinition<Reminder>>(), It.IsAny<FindOneAndUpdateOptions<Reminder, Reminder>>(), It.IsAny<CancellationToken>()))
            .Callback<FilterDefinition<Reminder>, UpdateDefinition<Reminder>, FindOneAndUpdateOptions<Reminder, Reminder>, CancellationToken>((_, update, _, _) => capturedUpdate = update)
            .ThrowsAsync(new InvalidOperationException());
        var service = new MongoRemindersService(collection.Object, Mock.Of<ILogger<MongoRemindersService>>());

        var result = await service.UpdateReminderAsync("id", "owner", "title", null, DateTime.UtcNow, null, "owner-id", null, null, "manager");

        Assert.That(result, Is.Null);
        Assert.That(capturedUpdate, Is.Not.Null);
        Assert.That(capturedUpdate!.Render(new RenderArgs<Reminder>(BsonSerializer.SerializerRegistry.GetSerializer<Reminder>(), BsonSerializer.SerializerRegistry)).ToJson(), Does.Contain("#ffeb3b"));
    }
}

[TestFixture]
public class MongoWorkerTypesServiceTests
{
    [Test]
    public async Task CreateAsync_InsertsAndReturnsSameWorkerType()
    {
        var collection = new Mock<IMongoCollection<WorkerType>>();
        collection.Setup(c => c.InsertOneAsync(It.IsAny<WorkerType>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var service = new MongoWorkerTypesService(collection.Object, Mock.Of<ILogger<MongoWorkerTypesService>>());
        var workerType = new WorkerType { Name = "Cook", OwnerId = "owner" };

        Assert.That(await service.CreateAsync(workerType), Is.SameAs(workerType));
        collection.Verify(c => c.InsertOneAsync(workerType, It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task DeleteAsync_ReturnsFalseOnDatabaseError()
    {
        var collection = new Mock<IMongoCollection<WorkerType>>();
        collection.Setup(c => c.DeleteOneAsync(It.IsAny<FilterDefinition<WorkerType>>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException());
        var service = new MongoWorkerTypesService(collection.Object, Mock.Of<ILogger<MongoWorkerTypesService>>());

        Assert.That(await service.DeleteAsync("id", "owner"), Is.False);
    }
}

[TestFixture]
public class MongoSchedulesServiceTests
{
    [Test]
    public async Task GetSchedulesByIdsAsync_EmptyIds_ReturnsEmptyWithoutQuery()
    {
        var collection = new Mock<IMongoCollection<Schedule>>();
        var service = new MongoSchedulesService(collection.Object, Mock.Of<ILogger<MongoSchedulesService>>());

        Assert.That(await service.GetSchedulesByIdsAsync([]), Is.Empty);
        Assert.That(collection.Invocations, Is.Empty);
    }

    [Test]
    public async Task SetDefaultScheduleAsync_ReturnsFalseWhenMongoUpdateFails()
    {
        var collection = new Mock<IMongoCollection<Schedule>>();
        collection.Setup(c => c.UpdateManyAsync(It.IsAny<FilterDefinition<Schedule>>(), It.IsAny<UpdateDefinition<Schedule>>(), It.IsAny<UpdateOptions>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException());
        var service = new MongoSchedulesService(collection.Object, Mock.Of<ILogger<MongoSchedulesService>>());

        Assert.That(await service.SetDefaultScheduleAsync("schedule", "manager"), Is.False);
    }

    [Test]
    public async Task DeleteScheduleAsync_DoesNotDeleteScheduleWhenShiftCleanupFails()
    {
        var collection = new Mock<IMongoCollection<Schedule>>();
        var schedulesCursor = new Mock<IAsyncCursor<Schedule>>();
        var schedules = new[]
        {
            new Schedule { Id = "507f1f77bcf86cd799439011", ManagerId = "manager-id" },
            new Schedule { Id = "507f1f77bcf86cd799439012", ManagerId = "manager-id" }
        };
        schedulesCursor.SetupSequence(c => c.MoveNext(It.IsAny<CancellationToken>())).Returns(true).Returns(false);
        schedulesCursor.SetupSequence(c => c.MoveNextAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true).ReturnsAsync(false);
        schedulesCursor.SetupGet(c => c.Current).Returns(schedules);
        var targetScheduleCursor = new Mock<IAsyncCursor<Schedule>>();
        targetScheduleCursor.SetupSequence(c => c.MoveNext(It.IsAny<CancellationToken>())).Returns(true).Returns(false);
        targetScheduleCursor.SetupSequence(c => c.MoveNextAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true).ReturnsAsync(false);
        targetScheduleCursor.SetupGet(c => c.Current).Returns([schedules[0]]);
        var cursors = new Queue<IAsyncCursor<Schedule>>([schedulesCursor.Object, targetScheduleCursor.Object]);
        collection.Setup(c => c.FindAsync(It.IsAny<FilterDefinition<Schedule>>(), It.IsAny<FindOptions<Schedule, Schedule>>(), It.IsAny<CancellationToken>()))
            .Returns(() => Task.FromResult(cursors.Dequeue()));
        collection.Setup(c => c.FindSync(It.IsAny<IClientSessionHandle>(), It.IsAny<FilterDefinition<Schedule>>(), It.IsAny<FindOptions<Schedule, Schedule>>(), It.IsAny<CancellationToken>()))
            .Returns(() => cursors.Dequeue());
        var shiftsService = new Mock<IShiftsService>();
        shiftsService.Setup(s => s.DeleteShiftsByScheduleIdAsync("507f1f77bcf86cd799439011"))
            .ThrowsAsync(new InvalidOperationException("Database unavailable"));
        var service = new MongoSchedulesService(collection.Object, Mock.Of<ILogger<MongoSchedulesService>>(), shiftsService.Object, Mock.Of<IAbsencesService>(), Mock.Of<IRemindersService>());

        var exception = Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.DeleteScheduleAsync("507f1f77bcf86cd799439011", "manager-id"));

        Assert.That(exception!.Message, Does.Contain("its shifts could not be deleted"));
        collection.Verify(c => c.DeleteOneAsync(It.IsAny<FilterDefinition<Schedule>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public void DeleteScheduleAsync_DoesNotDeleteScheduleWhenAbsenceCleanupFails()
    {
        var collection = new Mock<IMongoCollection<Schedule>>();
        var schedules = new[]
        {
            new Schedule { Id = "507f1f77bcf86cd799439011", ManagerId = "manager-id" },
            new Schedule { Id = "507f1f77bcf86cd799439012", ManagerId = "manager-id" }
        };
        IAsyncCursor<Schedule> NewCursor(Schedule[] items)
        {
            var cursor = new Mock<IAsyncCursor<Schedule>>();
            cursor.SetupSequence(c => c.MoveNext(It.IsAny<CancellationToken>())).Returns(true).Returns(false);
            cursor.SetupSequence(c => c.MoveNextAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true).ReturnsAsync(false);
            cursor.SetupGet(c => c.Current).Returns(items);
            return cursor.Object;
        }
        var cursors = new Queue<IAsyncCursor<Schedule>>([NewCursor(schedules), NewCursor([schedules[0]])]);
        collection.Setup(c => c.FindAsync(It.IsAny<FilterDefinition<Schedule>>(), It.IsAny<FindOptions<Schedule, Schedule>>(), It.IsAny<CancellationToken>()))
            .Returns(() => Task.FromResult(cursors.Dequeue()));
        var absences = new Mock<IAbsencesService>();
        absences.Setup(a => a.DeleteAbsencesByScheduleIdAsync("507f1f77bcf86cd799439011"))
            .ThrowsAsync(new InvalidOperationException("Database unavailable"));
        var service = new MongoSchedulesService(collection.Object, Mock.Of<ILogger<MongoSchedulesService>>(), Mock.Of<IShiftsService>(), absences.Object, Mock.Of<IRemindersService>());

        var exception = Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.DeleteScheduleAsync("507f1f77bcf86cd799439011", "manager-id"));

        Assert.That(exception!.Message, Does.Contain("its absences could not be deleted"));
        collection.Verify(c => c.DeleteOneAsync(It.IsAny<FilterDefinition<Schedule>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

}

[TestFixture]
public class MongoAbsencesServiceCoverageTests
{
    [Test]
    public async Task GetAbsencesAsync_WithoutOwnerOrManager_ReturnsEmptyWithoutQuery()
    {
        var collection = new Mock<IMongoCollection<Absence>>();
        var service = new MongoAbsencesService(collection.Object, Mock.Of<ILogger<MongoAbsencesService>>());

        var result = await service.GetAbsencesAsync(null, null, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));

        Assert.That(result, Is.Empty);
        Assert.That(collection.Invocations, Is.Empty);
    }

    [Test]
    public void CreateAbsenceAsync_RethrowsInsertFailure()
    {
        var collection = new Mock<IMongoCollection<Absence>>();
        collection.Setup(c => c.InsertOneAsync(It.IsAny<Absence>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException());
        var service = new MongoAbsencesService(collection.Object, Mock.Of<ILogger<MongoAbsencesService>>());

        Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAbsenceAsync(new Absence { Username = "owner" }));
    }

    [Test]
    public async Task DeleteAbsenceAsync_ReturnsFalseWhenMongoFails()
    {
        var collection = new Mock<IMongoCollection<Absence>>();
        collection.Setup(c => c.DeleteOneAsync(It.IsAny<FilterDefinition<Absence>>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException());
        var service = new MongoAbsencesService(collection.Object, Mock.Of<ILogger<MongoAbsencesService>>());

        Assert.That(await service.DeleteAbsenceAsync("id", "owner"), Is.False);
    }
}

[TestFixture]
public class MongoServiceConstructorTests
{
    [Test]
    public void MongoShiftsService_RequiresConnectionString()
    {
        var options = Microsoft.Extensions.Options.Options.Create(new MongoDbOptions { ConnectionString = " " });
        Assert.Throws<InvalidOperationException>(() => new MongoShiftsService(options, Mock.Of<ILogger<MongoShiftsService>>()));
    }

    [Test]
    public void MongoWorkerTypesService_RequiresConnectionString()
    {
        var options = Microsoft.Extensions.Options.Options.Create(new MongoDbOptions { ConnectionString = " " });
        Assert.Throws<InvalidOperationException>(() => new MongoWorkerTypesService(options, Mock.Of<ILogger<MongoWorkerTypesService>>()));
    }
}
