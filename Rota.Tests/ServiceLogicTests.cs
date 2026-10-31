using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using Rota.Models;
using Rota.Services;

namespace Rota.Tests;

[TestFixture]
public class RotationTemplateServiceTests
{
    [Test]
    public async Task ApplyAsync_ReturnsErrorWhenNoTemplateShiftsExist()
    {
        var shifts = new Mock<IShiftsService>();
        shifts.Setup(s => s.GetRotationTemplateShiftsAsync("rotation", "manager"))
            .ReturnsAsync(new List<Shift>());
        var service = new RotationTemplateService(shifts.Object);

        var result = await service.ApplyAsync("rotation", "owner", "manager", "schedule", [new DateOnly(2026, 10, 5)]);

        Assert.Multiple(() =>
        {
            Assert.That(result.CreatedCount, Is.Zero);
            Assert.That(result.Error, Is.EqualTo("No template shifts found for the selected WorkWeek."));
        });
        shifts.Verify(s => s.CreateShiftAsync(It.IsAny<Shift>()), Times.Never);
    }

    [Test]
    public async Task ApplyAsync_CreatesOnlyMatchingDayTemplatesAndCopiesFields()
    {
        var mondayTemplate = new Shift
        {
            WorkDay = DayOfWeek.Monday,
            Start = new DateTime(2000, 1, 3, 9, 30, 0),
            End = new DateTime(2000, 1, 3, 17, 0, 0),
            Title = "Opening",
            Notes = "front desk",
            Color = "#123456",
            WorkerType = "Reception",
            AssignedToUserId = "507f1f77bcf86cd799439011",
            RotationId = "507f1f77bcf86cd799439012",
        };
        var shifts = new Mock<IShiftsService>();
        shifts.Setup(s => s.GetRotationTemplateShiftsAsync("rotation", "manager"))
            .ReturnsAsync([mondayTemplate]);
        Shift? created = null;
        shifts.Setup(s => s.CreateShiftAsync(It.IsAny<Shift>()))
            .Callback<Shift>(shift => created = shift)
            .ReturnsAsync((Shift shift) => shift);
        var service = new RotationTemplateService(shifts.Object);
        var monday = new DateOnly(2026, 10, 5);
        var tuesday = monday.AddDays(1);

        var result = await service.ApplyAsync("rotation", "owner", "manager", "schedule", [monday, tuesday]);

        Assert.That(result, Is.EqualTo(new RotationApplyResult(1)));
        Assert.That(created, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(created!.Username, Is.EqualTo("owner"));
            Assert.That(created.ManagerCode, Is.EqualTo("manager"));
            Assert.That(created.ScheduleId, Is.EqualTo("schedule"));
            Assert.That(created.Title, Is.EqualTo("Opening"));
            Assert.That(created.Notes, Is.EqualTo("front desk"));
            Assert.That(created.Color, Is.EqualTo("#123456"));
            Assert.That(created.WorkerType, Is.EqualTo("Reception"));
            Assert.That(created.AssignedToUserId, Is.EqualTo(mondayTemplate.AssignedToUserId));
            Assert.That(created.RotationId, Is.Null);
            Assert.That(created.WorkDay, Is.Null);
            Assert.That(created.Start.ToLocalTime().TimeOfDay, Is.EqualTo(new TimeSpan(9, 30, 0)));
            Assert.That(created.Start.ToLocalTime().Date, Is.EqualTo(monday.ToDateTime(TimeOnly.MinValue).Date));
        });
        shifts.Verify(s => s.CreateShiftAsync(It.IsAny<Shift>()), Times.Once);
    }

    [Test]
    public async Task ApplyAsync_TreatsTemplateEndTimeAtMidnightAsNextDay()
    {
        var template = new Shift
        {
            WorkDay = DayOfWeek.Sunday,
            Start = new DateTime(2000, 1, 2, 16, 0, 0, DateTimeKind.Utc),
            End = new DateTime(2000, 1, 2, 0, 0, 0, DateTimeKind.Utc)
        };
        var shifts = new Mock<IShiftsService>();
        shifts.Setup(s => s.GetRotationTemplateShiftsAsync("rotation", "manager"))
            .ReturnsAsync([template]);
        Shift? created = null;
        shifts.Setup(s => s.CreateShiftAsync(It.IsAny<Shift>()))
            .Callback<Shift>(shift => created = shift)
            .ReturnsAsync((Shift shift) => shift);
        var service = new RotationTemplateService(shifts.Object);
        var sunday = new DateOnly(2026, 10, 4);

        var result = await service.ApplyAsync("rotation", "owner", "manager", "schedule", [sunday]);

        Assert.That(result.CreatedCount, Is.EqualTo(1));
        Assert.That(created, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(created!.Start.ToLocalTime().Date, Is.EqualTo(sunday.ToDateTime(TimeOnly.MinValue).Date));
            Assert.That(created.Start.ToLocalTime().TimeOfDay, Is.EqualTo(new TimeSpan(16, 0, 0)));
            Assert.That(created.End.ToLocalTime().Date, Is.EqualTo(sunday.AddDays(1).ToDateTime(TimeOnly.MinValue).Date));
            Assert.That(created.End.ToLocalTime().TimeOfDay, Is.EqualTo(TimeSpan.Zero));
        });
    }
}

[TestFixture]
public class ShiftAutoAssignServiceTests
{
    private static readonly DateOnly Monday = new(2026, 10, 5);

    [Test]
    public async Task AutoAssignAsync_ReturnsProvidedMessageWhenNoEligibleUnassignedShifts()
    {
        var shifts = new Mock<IShiftsService>();
        var users = new Mock<IUserService>();
        var absences = new Mock<IAbsencesService>();
        users.Setup(s => s.GetLinkedUsersForManagerAsync("manager")).ReturnsAsync([]);
        shifts.Setup(s => s.GetShiftsAsync("owner", "owner-id", "manager", It.IsAny<DateOnly>(), It.IsAny<DateOnly>()))
            .ReturnsAsync([]);
        absences.Setup(s => s.GetAbsencesAsync("manager", null, It.IsAny<DateOnly>(), It.IsAny<DateOnly>()))
            .ReturnsAsync([]);
        var service = new ShiftAutoAssignService(shifts.Object, users.Object, absences.Object);

        var outcome = await service.AutoAssignAsync("owner", "owner-id", "manager", Monday, Monday, null, "Nothing to do");

        Assert.Multiple(() =>
        {
            Assert.That(outcome.ShiftsChanged, Is.False);
            Assert.That(outcome.SuccessMessage, Is.EqualTo("Nothing to do"));
            Assert.That(outcome.WarningMessage, Is.Null);
            Assert.That(outcome.LinkedUsers, Is.Empty);
        });
        shifts.Verify(s => s.GetShiftsAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>()), Times.Once);
    }

    [Test]
    public async Task AutoAssignAsync_AssignsByPriorityAndReportsUncappedUsers()
    {
        var shift1 = CreateShift("shift1", Monday, "Cook");
        var shift2 = CreateShift("shift2", Monday.AddDays(1), "Cook");
        var manager = new User { Id = "manager-id", Username = "manager", Occupation = "Cook" };
        var employee = new User { Id = "employee-id", Username = "employee", DisplayName = "A Cook", Occupation = "cook", ShiftAssignmentPriority = 2, Availability = BuildFullWeekAvailability() };
        var capped = new User { Id = "capped-id", Username = "capped", Occupation = "Cook", ShiftAssignmentPriority = 1, MaxHours = 8, Availability = BuildFullWeekAvailability() };
        var users = new Mock<IUserService>();
        var absences = new Mock<IAbsencesService>();
        users.Setup(s => s.GetLinkedUsersForManagerAsync("manager")).ReturnsAsync([manager, employee, capped]);
        absences.Setup(s => s.GetAbsencesAsync("manager", null, It.IsAny<DateOnly>(), It.IsAny<DateOnly>()))
            .ReturnsAsync([]);
        var shifts = new Mock<IShiftsService>();
        SetupShiftLoads(shifts, [shift1, shift2], []);
        var assignedIds = new List<string?>();
        shifts.Setup(s => s.UpdateShiftAsync(It.IsAny<string>(), "owner", It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .Callback<string, string, DateTime, DateTime, string?, string?, string, string?, string?>((_, _, _, _, _, _, _, _, assignedId) => assignedIds.Add(assignedId))
            .ReturnsAsync((string id, string username, DateTime start, DateTime end, string? title, string? notes, string workerType, string? color, string? assignedId) => new Shift { Id = id, AssignedToUserId = assignedId });
        var service = new ShiftAutoAssignService(shifts.Object, users.Object, absences.Object);

        var outcome = await service.AutoAssignAsync("owner", "manager-id", "manager", Monday, Monday.AddDays(1), null, "No shifts");

        Assert.Multiple(() =>
        {
            Assert.That(outcome.ShiftsChanged, Is.True);
            Assert.That(outcome.SuccessMessage, Does.Contain("Successfully assigned 2 shifts"));
            Assert.That(outcome.SuccessMessage, Does.Contain("A Cook: 2 shifts (8h assigned)"));
            Assert.That(outcome.WarningMessage, Does.Contain("A Cook"));
            Assert.That(assignedIds, Is.EqualTo(new[] { "employee-id", "employee-id" }));
        });
        shifts.Verify(s => s.GetShiftsAsync("owner", "manager-id", "manager", It.IsAny<DateOnly>(), It.IsAny<DateOnly>()), Times.Exactly(2));
    }

    [Test]
    public async Task AutoAssignAsync_AssignsManagerWhenManagerMatchesShiftConstraints()
    {
        var managerShift = CreateShift("manager-shift", Monday, "Cook", 4);
        var manager = new User
        {
            Id = "manager-id",
            Username = "owner",
            Occupation = "Cook",
            MaxHours = 40,
            ShiftAssignmentPriority = 5,
            Availability = BuildFullWeekAvailability()
        };

        var users = new Mock<IUserService>();
        var absences = new Mock<IAbsencesService>();
        users.Setup(s => s.GetLinkedUsersForManagerAsync("manager")).ReturnsAsync([manager]);
        absences.Setup(s => s.GetAbsencesAsync("manager", null, It.IsAny<DateOnly>(), It.IsAny<DateOnly>())).ReturnsAsync([]);

        var shifts = new Mock<IShiftsService>();
        SetupShiftLoads(shifts, [managerShift], []);
        shifts.Setup(s => s.UpdateShiftAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .ReturnsAsync((string id, string username, DateTime start, DateTime end, string? title, string? notes, string workerType, string? color, string? assignedId) => new Shift { Id = id, AssignedToUserId = assignedId });
        var service = new ShiftAutoAssignService(shifts.Object, users.Object, absences.Object);

        var outcome = await service.AutoAssignAsync("owner", "manager-id", "manager", Monday, Monday, null, "No shifts");

        Assert.Multiple(() =>
        {
            Assert.That(outcome.ShiftsChanged, Is.True);
            Assert.That(outcome.SuccessMessage, Does.Contain("Successfully assigned 1 shift"));
            Assert.That(outcome.WarningMessage, Is.Null);
        });
        shifts.Verify(s => s.UpdateShiftAsync("manager-shift", "owner", It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string?>(), It.IsAny<string?>(), "Cook", It.IsAny<string?>(), "manager-id"), Times.Once);
    }

    [Test]
    public async Task AutoAssignAsync_RespectsIncludeDayAndMatchingOccupation()
    {
        var included = CreateShift("included", Monday, "Dishwasher");
        var excluded = CreateShift("excluded", Monday.AddDays(1), "Cook");
        var employee = new User { Id = "worker-id", Username = "worker", Occupation = "Dishwasher", MaxHours = 0, Availability = BuildFullWeekAvailability() };
        var users = new Mock<IUserService>();
        var absences = new Mock<IAbsencesService>();
        users.Setup(s => s.GetLinkedUsersForManagerAsync("manager")).ReturnsAsync([employee]);
        absences.Setup(s => s.GetAbsencesAsync("manager", null, It.IsAny<DateOnly>(), It.IsAny<DateOnly>()))
            .ReturnsAsync([]);
        var shifts = new Mock<IShiftsService>();
        SetupShiftLoads(shifts, [included, excluded], []);
        shifts.Setup(s => s.UpdateShiftAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .ReturnsAsync((string id, string username, DateTime start, DateTime end, string? title, string? notes, string workerType, string? color, string? assignedId) => new Shift { Id = id, AssignedToUserId = assignedId });
        var service = new ShiftAutoAssignService(shifts.Object, users.Object, absences.Object);

        var outcome = await service.AutoAssignAsync("owner", null, "manager", Monday, Monday.AddDays(1), day => day == Monday, "No shifts");

        Assert.Multiple(() =>
        {
            Assert.That(outcome.ShiftsChanged, Is.True);
            Assert.That(outcome.SuccessMessage, Does.Contain("Successfully assigned 1 shift"));
            Assert.That(outcome.WarningMessage, Does.Contain("worker"));
        });
        shifts.Verify(s => s.UpdateShiftAsync("included", "owner", It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string?>(), It.IsAny<string?>(), "Dishwasher", It.IsAny<string?>(), "worker-id"), Times.Once);
        shifts.Verify(s => s.UpdateShiftAsync("excluded", It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>()), Times.Never);
    }

    [Test]
    public async Task AutoAssignAsync_DoesNotExceedWeeklyHoursOrAssignSameEmployeeTwiceInADay()
    {
        var first = CreateShift("first", Monday, "Cook", 4);
        var sameDay = CreateShift("same-day", Monday, "Cook", 2);
        var laterDay = CreateShift("later", Monday.AddDays(1), "Cook", 4);
        var employee = new User { Id = "worker-id", Username = "worker", Occupation = "Cook", MaxHours = 6, Availability = BuildFullWeekAvailability() };
        var users = new Mock<IUserService>();
        var absences = new Mock<IAbsencesService>();
        users.Setup(s => s.GetLinkedUsersForManagerAsync("manager")).ReturnsAsync([employee]);
        absences.Setup(s => s.GetAbsencesAsync("manager", null, It.IsAny<DateOnly>(), It.IsAny<DateOnly>()))
            .ReturnsAsync([]);
        var shifts = new Mock<IShiftsService>();
        SetupShiftLoads(shifts, [first, sameDay, laterDay], [CreateShift("existing", Monday.AddDays(-1), "Cook", 2, "worker-id")]);
        var assigned = new List<string>();
        shifts.Setup(s => s.UpdateShiftAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .Callback<string, string, DateTime, DateTime, string?, string?, string, string?, string?>((id, _, _, _, _, _, _, _, _) => assigned.Add(id))
            .ReturnsAsync((string id, string username, DateTime start, DateTime end, string? title, string? notes, string workerType, string? color, string? assignedId) => new Shift { Id = id, AssignedToUserId = assignedId });
        var service = new ShiftAutoAssignService(shifts.Object, users.Object, absences.Object);

        var outcome = await service.AutoAssignAsync("owner", null, "manager", Monday, Monday.AddDays(1), null, "No shifts");

        Assert.That(assigned, Is.EqualTo(new[] { "first" }));
        Assert.That(outcome.SuccessMessage, Does.Contain("1 shift"));
        Assert.That(outcome.WarningMessage, Is.Null);
    }

    [Test]
    public async Task AutoAssignAsync_DoesNotAssignShiftOutsideEmployeeAvailability()
    {
        var earlyShift = CreateShiftAtTime("early", Monday, "Cook", new TimeOnly(6, 0), 4);
        var employee = new User
        {
            Id = "worker-id",
            Username = "worker",
            Occupation = "Cook",
            MaxHours = 40,
            Availability = [new UserAvailability { DayOfWeek = Monday.DayOfWeek, StartTime = "07:00", EndTime = "23:00" }]
        };

        var users = new Mock<IUserService>();
        var absences = new Mock<IAbsencesService>();
        users.Setup(s => s.GetLinkedUsersForManagerAsync("manager")).ReturnsAsync([employee]);
        absences.Setup(s => s.GetAbsencesAsync("manager", null, It.IsAny<DateOnly>(), It.IsAny<DateOnly>())).ReturnsAsync([]);

        var shifts = new Mock<IShiftsService>();
        SetupShiftLoads(shifts, [earlyShift], []);
        var service = new ShiftAutoAssignService(shifts.Object, users.Object, absences.Object);

        var outcome = await service.AutoAssignAsync("owner", null, "manager", Monday, Monday, null, "No shifts");

        Assert.Multiple(() =>
        {
            Assert.That(outcome.ShiftsChanged, Is.False);
            Assert.That(outcome.SuccessMessage, Does.Contain("No shifts could be assigned"));
        });
        shifts.Verify(s => s.UpdateShiftAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>()), Times.Never);
    }

    [Test]
    public async Task AutoAssignAsync_DoesNotAssignWhenApprovedAbsenceOverlapsShift()
    {
        var shift = CreateShiftAtTime("shift", Monday, "Cook", new TimeOnly(9, 0), 4);
        var employee = new User { Id = "worker-id", Username = "worker", Occupation = "Cook", Availability = BuildFullWeekAvailability(), MaxHours = 40 };

        var approvedAbsence = new Absence
        {
            AssignedToUserId = "worker-id",
            UserId = "worker-id",
            ApprovalState = AbsenceApprovalState.Approved,
            StartDate = Monday.ToDateTime(new TimeOnly(12, 0), DateTimeKind.Local).ToUniversalTime(),
            EndDate = Monday.ToDateTime(new TimeOnly(15, 0), DateTimeKind.Local).ToUniversalTime()
        };

        var users = new Mock<IUserService>();
        var absences = new Mock<IAbsencesService>();
        users.Setup(s => s.GetLinkedUsersForManagerAsync("manager")).ReturnsAsync([employee]);
        absences.Setup(s => s.GetAbsencesAsync("manager", null, It.IsAny<DateOnly>(), It.IsAny<DateOnly>())).ReturnsAsync([approvedAbsence]);

        var shifts = new Mock<IShiftsService>();
        SetupShiftLoads(shifts, [shift], []);
        var service = new ShiftAutoAssignService(shifts.Object, users.Object, absences.Object);

        var outcome = await service.AutoAssignAsync("owner", null, "manager", Monday, Monday, null, "No shifts");

        Assert.That(outcome.ShiftsChanged, Is.False);
        shifts.Verify(s => s.UpdateShiftAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>()), Times.Never);
    }

    [Test]
    public async Task AutoAssignAsync_ResetsWeeklyMaxHoursOnSundayBoundary()
    {
        var saturday = Monday.AddDays(-2);
        var sunday = Monday.AddDays(-1);

        var saturdayShift = CreateShift("sat-shift", saturday, "Cook", 4);
        var sundayShift = CreateShift("sun-shift", sunday, "Cook", 4);

        var employee = new User { Id = "worker-id", Username = "worker", Occupation = "Cook", MaxHours = 4, Availability = BuildFullWeekAvailability() };

        var users = new Mock<IUserService>();
        var absences = new Mock<IAbsencesService>();
        users.Setup(s => s.GetLinkedUsersForManagerAsync("manager")).ReturnsAsync([employee]);
        absences.Setup(s => s.GetAbsencesAsync("manager", null, It.IsAny<DateOnly>(), It.IsAny<DateOnly>())).ReturnsAsync([]);

        var shifts = new Mock<IShiftsService>();
        SetupShiftLoads(shifts, [saturdayShift, sundayShift], []);
        var assigned = new List<string>();
        shifts.Setup(s => s.UpdateShiftAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .Callback<string, string, DateTime, DateTime, string?, string?, string, string?, string?>((id, _, _, _, _, _, _, _, _) => assigned.Add(id))
            .ReturnsAsync((string id, string username, DateTime start, DateTime end, string? title, string? notes, string workerType, string? color, string? assignedId) => new Shift { Id = id, AssignedToUserId = assignedId });

        var service = new ShiftAutoAssignService(shifts.Object, users.Object, absences.Object);

        var outcome = await service.AutoAssignAsync("owner", null, "manager", saturday, sunday, null, "No shifts");

        Assert.Multiple(() =>
        {
            Assert.That(outcome.ShiftsChanged, Is.True);
            Assert.That(assigned, Is.EqualTo(new[] { "sat-shift", "sun-shift" }));
        });
    }

    private static Shift CreateShift(string id, DateOnly day, string workerType, double hours = 4, string? assignedTo = null)
    {
        var start = day.ToDateTime(new TimeOnly(9, 0), DateTimeKind.Local);
        return new Shift
        {
            Id = id,
            Username = "owner",
            Start = start.ToUniversalTime(),
            End = start.AddHours(hours).ToUniversalTime(),
            WorkerType = workerType,
            AssignedToUserId = assignedTo
        };
    }

    private static Shift CreateShiftAtTime(string id, DateOnly day, string workerType, TimeOnly startTime, double hours = 4, string? assignedTo = null)
    {
        var start = day.ToDateTime(startTime, DateTimeKind.Local);
        return new Shift
        {
            Id = id,
            Username = "owner",
            Start = start.ToUniversalTime(),
            End = start.AddHours(hours).ToUniversalTime(),
            WorkerType = workerType,
            AssignedToUserId = assignedTo
        };
    }

    private static List<UserAvailability> BuildFullWeekAvailability()
    {
        return Enum.GetValues<DayOfWeek>()
            .Select(day => new UserAvailability
            {
                DayOfWeek = day,
                StartTime = "00:00",
                EndTime = "23:59"
            })
            .ToList();
    }

    private static void SetupShiftLoads(Mock<IShiftsService> shifts, List<Shift> requested, List<Shift> fullWeek)
    {
        shifts.SetupSequence(s => s.GetShiftsAsync("owner", It.IsAny<string?>(), "manager", It.IsAny<DateOnly>(), It.IsAny<DateOnly>()))
            .ReturnsAsync(requested)
            .ReturnsAsync(fullWeek);
    }
}

[TestFixture]
public class HttpContextAuthenticationStateProviderTests
{
    [Test]
    public async Task GetAuthenticationStateAsync_UsesCurrentHttpContextPrincipal()
    {
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, "manager")], "test");
        var context = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
        var accessor = new Mock<IHttpContextAccessor>();
        accessor.SetupGet(a => a.HttpContext).Returns(context);
        var provider = new HttpContextAuthenticationStateProvider(accessor.Object, Mock.Of<ILogger<HttpContextAuthenticationStateProvider>>());

        var state = await provider.GetAuthenticationStateAsync();

        Assert.That(state.User.Identity?.Name, Is.EqualTo("manager"));
        Assert.That(state.User.Identity?.IsAuthenticated, Is.True);
    }

    [Test]
    public async Task GetAuthenticationStateAsync_ReturnsAnonymousWhenNoHttpContextExists()
    {
        var accessor = new Mock<IHttpContextAccessor>();
        accessor.SetupGet(a => a.HttpContext).Returns((HttpContext?)null);
        var provider = new HttpContextAuthenticationStateProvider(accessor.Object, Mock.Of<ILogger<HttpContextAuthenticationStateProvider>>());

        var state = await provider.GetAuthenticationStateAsync();

        Assert.That(state.User.Identity?.IsAuthenticated, Is.False);
    }
}
