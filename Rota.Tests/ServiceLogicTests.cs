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
    public async Task ApplyAsync_ReturnsErrorWhenNoTemplateWorkItemsExist()
    {
        var WorkItems = new Mock<IWorkItemsService>();
        WorkItems.Setup(s => s.GetRotationTemplateWorkItemsAsync("rotation", "manager"))
            .ReturnsAsync(new List<WorkItem>());
        var service = new RotationTemplateService(WorkItems.Object);

        var result = await service.ApplyAsync("rotation", "owner", "manager", "schedule", [new DateOnly(2026, 10, 5)]);

        Assert.Multiple(() =>
        {
            Assert.That(result.CreatedCount, Is.Zero);
            Assert.That(result.Error, Is.EqualTo("No template work items found for the selected WorkWeek."));
        });
        WorkItems.Verify(s => s.CreateWorkItemAsync(It.IsAny<WorkItem>()), Times.Never);
    }

    [Test]
    public async Task ApplyAsync_CreatesOnlyMatchingDayTemplatesAndCopiesFields()
    {
        var mondayTemplate = new WorkItem
        {
            WorkDay = DayOfWeek.Monday,
            Start = new DateTime(2000, 1, 3, 9, 30, 0, DateTimeKind.Utc),
            End = new DateTime(2000, 1, 3, 17, 0, 0, DateTimeKind.Utc),
            Title = "Opening",
            Notes = "front desk",
            Color = "#123456",
            WorkerType = "Reception",
            AssignedToUserId = "507f1f77bcf86cd799439011",
            RotationId = "507f1f77bcf86cd799439012",
        };
        var WorkItems = new Mock<IWorkItemsService>();
        WorkItems.Setup(s => s.GetRotationTemplateWorkItemsAsync("rotation", "manager"))
            .ReturnsAsync([mondayTemplate]);
        WorkItem? created = null;
        WorkItems.Setup(s => s.CreateWorkItemAsync(It.IsAny<WorkItem>()))
            .Callback<WorkItem>(WorkItem => created = WorkItem)
            .ReturnsAsync((WorkItem WorkItem) => WorkItem);
        var service = new RotationTemplateService(WorkItems.Object);
        var monday = new DateOnly(2026, 10, 5);
        var tuesday = monday.AddDays(1);

        var result = await service.ApplyAsync("rotation", "owner", "manager", "schedule", [monday, tuesday]);

        Assert.That(result, Is.EqualTo(new RotationApplyResult(1)));
        Assert.That(created, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(created!.Username, Is.EqualTo("owner"));
            Assert.That(created.TeamCode, Is.EqualTo("manager"));
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
        WorkItems.Verify(s => s.CreateWorkItemAsync(It.IsAny<WorkItem>()), Times.Once);
    }

    [Test]
    public async Task ApplyAsync_TreatsTemplateEndTimeAtMidnightAsNextDay()
    {
        var template = new WorkItem
        {
            WorkDay = DayOfWeek.Sunday,
            Start = new DateTime(2000, 1, 2, 16, 0, 0, DateTimeKind.Utc),
            End = new DateTime(2000, 1, 2, 0, 0, 0, DateTimeKind.Utc)
        };
        var WorkItems = new Mock<IWorkItemsService>();
        WorkItems.Setup(s => s.GetRotationTemplateWorkItemsAsync("rotation", "manager"))
            .ReturnsAsync([template]);
        WorkItem? created = null;
        WorkItems.Setup(s => s.CreateWorkItemAsync(It.IsAny<WorkItem>()))
            .Callback<WorkItem>(WorkItem => created = WorkItem)
            .ReturnsAsync((WorkItem WorkItem) => WorkItem);
        var service = new RotationTemplateService(WorkItems.Object);
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
public class WorkItemAutoAssignServiceTests
{
    private static readonly DateOnly Monday = new(2026, 10, 5);

    [Test]
    public async Task AutoAssignAsync_ReturnsProvidedMessageWhenNoEligibleUnassignedWorkItems()
    {
        var WorkItems = new Mock<IWorkItemsService>();
        var users = new Mock<IUserService>();
        var absences = new Mock<IAbsencesService>();
        users.Setup(s => s.GetLinkedUsersForManagerAsync("manager")).ReturnsAsync([]);
        WorkItems.Setup(s => s.GetWorkItemsAsync("owner", "owner-id", "manager", It.IsAny<DateOnly>(), It.IsAny<DateOnly>()))
            .ReturnsAsync([]);
        absences.Setup(s => s.GetAbsencesAsync("manager", null, It.IsAny<DateOnly>(), It.IsAny<DateOnly>()))
            .ReturnsAsync([]);
        var service = new WorkItemAutoAssignService(WorkItems.Object, users.Object, absences.Object);

        var outcome = await service.AutoAssignAsync("owner", "owner-id", "manager", Monday, Monday, null, "Nothing to do");

        Assert.Multiple(() =>
        {
            Assert.That(outcome.WorkItemsChanged, Is.False);
            Assert.That(outcome.SuccessMessage, Is.EqualTo("Nothing to do"));
            Assert.That(outcome.WarningMessage, Is.Null);
            Assert.That(outcome.LinkedUsers, Is.Empty);
        });
        WorkItems.Verify(s => s.GetWorkItemsAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>()), Times.Once);
    }

    [Test]
    public async Task AutoAssignAsync_AssignsByPriorityAndReportsUncappedUsers()
    {
        var WorkItem1 = CreateWorkItem("Work item1", Monday, "Cook");
        var WorkItem2 = CreateWorkItem("Work item2", Monday.AddDays(1), "Cook");
        var manager = new User { Id = "manager-id", Username = "manager", TeamMemberType = "Cook" };
        var employee = new User { Id = "employee-id", Username = "employee", DisplayName = "A Cook", TeamMemberType = "cook", WorkItemAssignmentPriority = 2, Availability = BuildFullWeekAvailability() };
        var capped = new User { Id = "capped-id", Username = "capped", TeamMemberType = "Cook", WorkItemAssignmentPriority = 1, MaxHours = 8, Availability = BuildFullWeekAvailability() };
        var users = new Mock<IUserService>();
        var absences = new Mock<IAbsencesService>();
        users.Setup(s => s.GetLinkedUsersForManagerAsync("manager")).ReturnsAsync([manager, employee, capped]);
        absences.Setup(s => s.GetAbsencesAsync("manager", null, It.IsAny<DateOnly>(), It.IsAny<DateOnly>()))
            .ReturnsAsync([]);
        var WorkItems = new Mock<IWorkItemsService>();
        SetupWorkItemLoads(WorkItems, [WorkItem1, WorkItem2], []);
        var assignedIds = new List<string?>();
        WorkItems.Setup(s => s.UpdateWorkItemAsync(It.IsAny<string>(), "owner", It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .Callback<string, string, DateTime, DateTime, string?, string?, string, string?, string?>((_, _, _, _, _, _, _, _, assignedId) => assignedIds.Add(assignedId))
            .ReturnsAsync((string id, string username, DateTime start, DateTime end, string? title, string? notes, string workerType, string? color, string? assignedId) => new WorkItem { Id = id, AssignedToUserId = assignedId });
        var service = new WorkItemAutoAssignService(WorkItems.Object, users.Object, absences.Object);

        var outcome = await service.AutoAssignAsync("owner", "manager-id", "manager", Monday, Monday.AddDays(1), null, "No work items");

        Assert.Multiple(() =>
        {
            Assert.That(outcome.WorkItemsChanged, Is.True);
            Assert.That(outcome.SuccessMessage, Does.Contain("Successfully assigned 2 work items"));
            Assert.That(outcome.SuccessMessage, Does.Contain("A Cook: 2 work items (8h assigned)"));
            Assert.That(outcome.WarningMessage, Does.Contain("A Cook"));
            Assert.That(assignedIds, Is.EqualTo(new[] { "employee-id", "employee-id" }));
        });
        WorkItems.Verify(s => s.GetWorkItemsAsync("owner", "manager-id", "manager", It.IsAny<DateOnly>(), It.IsAny<DateOnly>()), Times.Exactly(2));
    }

    [Test]
    public async Task AutoAssignAsync_AssignsManagerWhenManagerMatchesWorkItemConstraints()
    {
        var managerWorkItem = CreateWorkItem("manager-work-item", Monday, "Cook", 4);
        var manager = new User
        {
            Id = "manager-id",
            Username = "owner",
            TeamMemberType = "Cook",
            MaxHours = 40,
            WorkItemAssignmentPriority = 5,
            Availability = BuildFullWeekAvailability()
        };

        var users = new Mock<IUserService>();
        var absences = new Mock<IAbsencesService>();
        users.Setup(s => s.GetLinkedUsersForManagerAsync("manager")).ReturnsAsync([manager]);
        absences.Setup(s => s.GetAbsencesAsync("manager", null, It.IsAny<DateOnly>(), It.IsAny<DateOnly>())).ReturnsAsync([]);

        var WorkItems = new Mock<IWorkItemsService>();
        SetupWorkItemLoads(WorkItems, [managerWorkItem], []);
        WorkItems.Setup(s => s.UpdateWorkItemAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .ReturnsAsync((string id, string username, DateTime start, DateTime end, string? title, string? notes, string workerType, string? color, string? assignedId) => new WorkItem { Id = id, AssignedToUserId = assignedId });
        var service = new WorkItemAutoAssignService(WorkItems.Object, users.Object, absences.Object);

        var outcome = await service.AutoAssignAsync("owner", "manager-id", "manager", Monday, Monday, null, "No work items");

        Assert.Multiple(() =>
        {
            Assert.That(outcome.WorkItemsChanged, Is.True);
            Assert.That(outcome.SuccessMessage, Does.Contain("Successfully assigned 1 work item"));
            Assert.That(outcome.WarningMessage, Is.Null);
        });
        WorkItems.Verify(s => s.UpdateWorkItemAsync("manager-work-item", "owner", It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string?>(), It.IsAny<string?>(), "Cook", It.IsAny<string?>(), "manager-id"), Times.Once);
    }

    [Test]
    public async Task AutoAssignAsync_RespectsIncludeDayAndMatchingTeamMemberType()
    {
        var included = CreateWorkItem("included", Monday, "Dishwasher");
        var excluded = CreateWorkItem("excluded", Monday.AddDays(1), "Cook");
        var employee = new User { Id = "worker-id", Username = "worker", TeamMemberType = "Dishwasher", MaxHours = 0, Availability = BuildFullWeekAvailability() };
        var users = new Mock<IUserService>();
        var absences = new Mock<IAbsencesService>();
        users.Setup(s => s.GetLinkedUsersForManagerAsync("manager")).ReturnsAsync([employee]);
        absences.Setup(s => s.GetAbsencesAsync("manager", null, It.IsAny<DateOnly>(), It.IsAny<DateOnly>()))
            .ReturnsAsync([]);
        var WorkItems = new Mock<IWorkItemsService>();
        SetupWorkItemLoads(WorkItems, [included, excluded], []);
        WorkItems.Setup(s => s.UpdateWorkItemAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .ReturnsAsync((string id, string username, DateTime start, DateTime end, string? title, string? notes, string workerType, string? color, string? assignedId) => new WorkItem { Id = id, AssignedToUserId = assignedId });
        var service = new WorkItemAutoAssignService(WorkItems.Object, users.Object, absences.Object);

        var outcome = await service.AutoAssignAsync("owner", null, "manager", Monday, Monday.AddDays(1), day => day == Monday, "No work items");

        Assert.Multiple(() =>
        {
            Assert.That(outcome.WorkItemsChanged, Is.True);
            Assert.That(outcome.SuccessMessage, Does.Contain("Successfully assigned 1 work item"));
            Assert.That(outcome.WarningMessage, Does.Contain("worker"));
        });
        WorkItems.Verify(s => s.UpdateWorkItemAsync("included", "owner", It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string?>(), It.IsAny<string?>(), "Dishwasher", It.IsAny<string?>(), "worker-id"), Times.Once);
        WorkItems.Verify(s => s.UpdateWorkItemAsync("excluded", It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>()), Times.Never);
    }

    [Test]
    public async Task AutoAssignAsync_DoesNotExceedWeeklyHoursOrAssignSameEmployeeTwiceInADay()
    {
        var first = CreateWorkItem("first", Monday, "Cook", 4);
        var sameDay = CreateWorkItem("same-day", Monday, "Cook", 2);
        var laterDay = CreateWorkItem("later", Monday.AddDays(1), "Cook", 4);
        var employee = new User { Id = "worker-id", Username = "worker", TeamMemberType = "Cook", MaxHours = 6, Availability = BuildFullWeekAvailability() };
        var users = new Mock<IUserService>();
        var absences = new Mock<IAbsencesService>();
        users.Setup(s => s.GetLinkedUsersForManagerAsync("manager")).ReturnsAsync([employee]);
        absences.Setup(s => s.GetAbsencesAsync("manager", null, It.IsAny<DateOnly>(), It.IsAny<DateOnly>()))
            .ReturnsAsync([]);
        var WorkItems = new Mock<IWorkItemsService>();
        SetupWorkItemLoads(WorkItems, [first, sameDay, laterDay], [CreateWorkItem("existing", Monday.AddDays(-1), "Cook", 2, "worker-id")]);
        var assigned = new List<string>();
        WorkItems.Setup(s => s.UpdateWorkItemAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .Callback<string, string, DateTime, DateTime, string?, string?, string, string?, string?>((id, _, _, _, _, _, _, _, _) => assigned.Add(id))
            .ReturnsAsync((string id, string username, DateTime start, DateTime end, string? title, string? notes, string workerType, string? color, string? assignedId) => new WorkItem { Id = id, AssignedToUserId = assignedId });
        var service = new WorkItemAutoAssignService(WorkItems.Object, users.Object, absences.Object);

        var outcome = await service.AutoAssignAsync("owner", null, "manager", Monday, Monday.AddDays(1), null, "No work items");

        Assert.That(assigned, Is.EqualTo(new[] { "first" }));
        Assert.That(outcome.SuccessMessage, Does.Contain("1 work item"));
        Assert.That(outcome.WarningMessage, Is.Null);
    }

    [Test]
    public async Task AutoAssignAsync_DoesNotAssignWorkItemOutsideEmployeeAvailability()
    {
        var earlyWorkItem = CreateWorkItemAtTime("early", Monday, "Cook", new TimeOnly(6, 0), 4);
        var employee = new User
        {
            Id = "worker-id",
            Username = "worker",
            TeamMemberType = "Cook",
            MaxHours = 40,
            Availability = [new UserAvailability { DayOfWeek = Monday.DayOfWeek, StartTime = "07:00", EndTime = "23:00" }]
        };

        var users = new Mock<IUserService>();
        var absences = new Mock<IAbsencesService>();
        users.Setup(s => s.GetLinkedUsersForManagerAsync("manager")).ReturnsAsync([employee]);
        absences.Setup(s => s.GetAbsencesAsync("manager", null, It.IsAny<DateOnly>(), It.IsAny<DateOnly>())).ReturnsAsync([]);

        var WorkItems = new Mock<IWorkItemsService>();
        SetupWorkItemLoads(WorkItems, [earlyWorkItem], []);
        var service = new WorkItemAutoAssignService(WorkItems.Object, users.Object, absences.Object);

        var outcome = await service.AutoAssignAsync("owner", null, "manager", Monday, Monday, null, "No work items");

        Assert.Multiple(() =>
        {
            Assert.That(outcome.WorkItemsChanged, Is.False);
            Assert.That(outcome.SuccessMessage, Does.Contain("No work items could be assigned"));
        });
        WorkItems.Verify(s => s.UpdateWorkItemAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>()), Times.Never);
    }

    [Test]
    public async Task AutoAssignAsync_DoesNotAssignWhenApprovedAbsenceOverlapsWorkItem()
    {
        var WorkItem = CreateWorkItemAtTime("Work item", Monday, "Cook", new TimeOnly(9, 0), 4);
        var employee = new User { Id = "worker-id", Username = "worker", TeamMemberType = "Cook", Availability = BuildFullWeekAvailability(), MaxHours = 40 };

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

        var WorkItems = new Mock<IWorkItemsService>();
        SetupWorkItemLoads(WorkItems, [WorkItem], []);
        var service = new WorkItemAutoAssignService(WorkItems.Object, users.Object, absences.Object);

        var outcome = await service.AutoAssignAsync("owner", null, "manager", Monday, Monday, null, "No work items");

        Assert.That(outcome.WorkItemsChanged, Is.False);
        WorkItems.Verify(s => s.UpdateWorkItemAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>()), Times.Never);
    }

    [Test]
    public async Task AutoAssignAsync_ResetsWeeklyMaxHoursOnSundayBoundary()
    {
        var saturday = Monday.AddDays(-2);
        var sunday = Monday.AddDays(-1);

        var saturdayWorkItem = CreateWorkItem("sat-work-item", saturday, "Cook", 4);
        var sundayWorkItem = CreateWorkItem("sun-work-item", sunday, "Cook", 4);

        var employee = new User { Id = "worker-id", Username = "worker", TeamMemberType = "Cook", MaxHours = 4, Availability = BuildFullWeekAvailability() };

        var users = new Mock<IUserService>();
        var absences = new Mock<IAbsencesService>();
        users.Setup(s => s.GetLinkedUsersForManagerAsync("manager")).ReturnsAsync([employee]);
        absences.Setup(s => s.GetAbsencesAsync("manager", null, It.IsAny<DateOnly>(), It.IsAny<DateOnly>())).ReturnsAsync([]);

        var WorkItems = new Mock<IWorkItemsService>();
        SetupWorkItemLoads(WorkItems, [saturdayWorkItem, sundayWorkItem], []);
        var assigned = new List<string>();
        WorkItems.Setup(s => s.UpdateWorkItemAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .Callback<string, string, DateTime, DateTime, string?, string?, string, string?, string?>((id, _, _, _, _, _, _, _, _) => assigned.Add(id))
            .ReturnsAsync((string id, string username, DateTime start, DateTime end, string? title, string? notes, string workerType, string? color, string? assignedId) => new WorkItem { Id = id, AssignedToUserId = assignedId });

        var service = new WorkItemAutoAssignService(WorkItems.Object, users.Object, absences.Object);

        var outcome = await service.AutoAssignAsync("owner", null, "manager", saturday, sunday, null, "No work items");

        Assert.Multiple(() =>
        {
            Assert.That(outcome.WorkItemsChanged, Is.True);
            Assert.That(assigned, Is.EqualTo(new[] { "sat-work-item", "sun-work-item" }));
        });
    }

    private static WorkItem CreateWorkItem(string id, DateOnly day, string workerType, double hours = 4, string? assignedTo = null)
    {
        var start = day.ToDateTime(new TimeOnly(9, 0), DateTimeKind.Local);
        return new WorkItem
        {
            Id = id,
            Username = "owner",
            Start = start.ToUniversalTime(),
            End = start.AddHours(hours).ToUniversalTime(),
            WorkerType = workerType,
            AssignedToUserId = assignedTo
        };
    }

    private static WorkItem CreateWorkItemAtTime(string id, DateOnly day, string workerType, TimeOnly startTime, double hours = 4, string? assignedTo = null)
    {
        var start = day.ToDateTime(startTime, DateTimeKind.Local);
        return new WorkItem
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

    private static void SetupWorkItemLoads(Mock<IWorkItemsService> WorkItems, List<WorkItem> requested, List<WorkItem> fullWeek)
    {
        WorkItems.SetupSequence(s => s.GetWorkItemsAsync("owner", It.IsAny<string?>(), "manager", It.IsAny<DateOnly>(), It.IsAny<DateOnly>()))
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
