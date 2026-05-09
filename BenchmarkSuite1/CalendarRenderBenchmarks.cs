using BenchmarkDotNet.Attributes;
using Rota.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.VSDiagnostics;

namespace Rota.Benchmarks;
[ShortRunJob]
[CPUUsageDiagnoser]
public class CalendarRenderBenchmarks
{
    // Simulate a realistic load: 200 shifts, 50 absences, 40 reminders over a 6-week view window
    private List<Shift> _shifts = null!;
    private List<Absence> _absences = null!;
    private List<Reminder> _reminders = null!;
    private List<User> _employees = null!;
    private List<User> _linkedUsers = null!;
    private Dictionary<string, bool> _workerTypeVisibility = null!;
    private Dictionary<string, bool> _employeeVisibility = null!;
    private DateOnly _current;
    private DateOnly _targetDay;
    [GlobalSetup]
    public void Setup()
    {
        _current = DateOnly.FromDateTime(DateTime.Today);
        _targetDay = _current;
        var rng = new Random(42);
        var employeeIds = Enumerable.Range(1, 10).Select(i => $"emp{i:D3}").ToList();
        var workerTypes = new[]
        {
            "General",
            "Nurse",
            "Driver",
            "Admin",
            "Security"
        };
        // Generate employees
        _employees = employeeIds.Select((id, i) => new User { Id = id, Username = $"user{i}", DisplayName = $"Employee {i}", FirstName = $"First{i}", LastName = $"Last{i}" }).ToList();
        _linkedUsers = _employees.ToList();
        // Generate shifts spread over a 6-week window
        var windowStart = StartOfWeek(_current).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        _shifts = Enumerable.Range(0, 200).Select(i =>
        {
            var dayOffset = rng.Next(0, 42);
            var startDt = windowStart.AddDays(dayOffset).AddHours(rng.Next(6, 14));
            return new Shift
            {
                Id = $"shift{i}",
                Username = "manager1",
                Start = startDt,
                End = startDt.AddHours(rng.Next(4, 10)),
                Title = $"Shift {i}",
                WorkerType = workerTypes[rng.Next(workerTypes.Length)],
                Color = "#1890ff",
                AssignedToUserId = rng.Next(0, 3) == 0 ? null : employeeIds[rng.Next(employeeIds.Count)],
                ScheduleId = "sched001",
                ManagerCode = "MGR001"
            };
        }).ToList();
        // Generate absences
        _absences = Enumerable.Range(0, 50).Select(i =>
        {
            var dayOffset = rng.Next(0, 42);
            var startDt = windowStart.AddDays(dayOffset);
            return new Absence
            {
                Id = $"absence{i}",
                Username = "manager1",
                StartDate = startDt,
                EndDate = startDt.AddDays(1),
                DayCount = 1,
                Title = $"Absence {i}",
                Color = "#fa8c16",
                AssignedToUserId = employeeIds[rng.Next(employeeIds.Count)]
            };
        }).ToList();
        // Generate reminders
        _reminders = Enumerable.Range(0, 40).Select(i =>
        {
            var dayOffset = rng.Next(0, 42);
            return new Reminder
            {
                Id = $"reminder{i}",
                Username = "manager1",
                Date = windowStart.AddDays(dayOffset).AddHours(rng.Next(8, 18)),
                Title = $"Reminder {i}",
                Color = "#ffeb3b",
                ManagerCode = "MGR001",
                ScheduleId = "sched001"
            };
        }).ToList();
        // All worker types visible
        _workerTypeVisibility = workerTypes.ToDictionary(wt => wt, _ => true, StringComparer.OrdinalIgnoreCase);
        _employeeVisibility = employeeIds.Concat(new[] { "__unassigned__" }).ToDictionary(id => id, _ => true, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Full month-view render simulation: GetSortedDayItems called for all 42 day cells.</summary>
    [Benchmark(Baseline = true)]
    public int MonthViewRender()
    {
        var matrix = GetMonthMatrix(_current);
        int totalItems = 0;
        foreach (var week in matrix)
            foreach (var day in week)
                totalItems += GetSortedDayItems(day).Count;
        return totalItems;
    }

    /// <summary>Cost of GetShiftsForDay for a single day (called 42× per month render).</summary>
    [Benchmark]
    public int GetShiftsForDaySingle()
    {
        return GetShiftsForDay(_targetDay).Count;
    }

    /// <summary>Cost of GetSortedDayItems for a single day.</summary>
    [Benchmark]
    public int GetSortedDayItemsSingle()
    {
        return GetSortedDayItems(_targetDay).Count;
    }

    /// <summary>Cost of GetEmployeeDisplayNameById called for each shift (≈200 calls per render).</summary>
    [Benchmark]
    public int EmployeeNameLookups()
    {
        int count = 0;
        foreach (var s in _shifts)
        {
            var name = GetEmployeeDisplayNameById(s.AssignedToUserId);
            if (name.Length > 0)
                count++;
        }

        return count;
    }

    // ── Helpers mirroring Calendar.razor logic ────────────────────────────
    private List<Shift> GetShiftsForDay(DateOnly day)
    {
        var startUtc = day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Local).ToUniversalTime();
        var endUtc = day.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Local).ToUniversalTime();
        return _shifts.Where(s => s.Start < endUtc && s.End > startUtc).Where(s => string.IsNullOrEmpty("sched001") || s.ScheduleId == "sched001").Where(s =>
        {
            if (_workerTypeVisibility.Count == 0)
                return true;
            var wt = string.IsNullOrWhiteSpace(s.WorkerType) ? "General" : s.WorkerType;
            return _workerTypeVisibility.TryGetValue(wt, out var visible) && visible;
        }).Where(s =>
        {
            if (_employeeVisibility.Count == 0)
                return true;
            var empKey = string.IsNullOrWhiteSpace(s.AssignedToUserId) ? "__unassigned__" : s.AssignedToUserId;
            return _employeeVisibility.TryGetValue(empKey, out var visible) && visible;
        }).ToList();
    }

    private List<Absence> GetAbsencesForDay(DateOnly day)
    {
        var startUtc = day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Local).ToUniversalTime();
        var endUtc = day.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Local).ToUniversalTime();
        return _absences.Where(a => a.StartDate < endUtc && a.EndDate > startUtc).ToList();
    }

    private List<Reminder> GetRemindersForDay(DateOnly day)
    {
        var startLocal = day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Local);
        var endLocal = day.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Local);
        var startUtc = startLocal.ToUniversalTime();
        var endUtc = endLocal.ToUniversalTime();
        return _reminders.Where(r => r.ReminderDateTime >= startUtc && r.ReminderDateTime < endUtc).Where(r => r.ScheduleId == "sched001").ToList();
    }

    private sealed record DayItem(DateTime SortKey, Shift? Shift = null, Absence? Absence = null, Reminder? Reminder = null);
    private List<DayItem> GetSortedDayItems(DateOnly day)
    {
        var items = new List<DayItem>();
        foreach (var s in GetShiftsForDay(day))
            items.Add(new DayItem(s.Start, Shift: s));
        foreach (var a in GetAbsencesForDay(day))
        {
            var sortKey = a.StartDate;
            if (a.DayCount == 1 && !string.IsNullOrWhiteSpace(a.StartTime) && TimeOnly.TryParse(a.StartTime, CultureInfo.CurrentCulture, out var t))
                sortKey = day.ToDateTime(t, DateTimeKind.Local).ToUniversalTime();
            items.Add(new DayItem(sortKey, Absence: a));
        }

        foreach (var r in GetRemindersForDay(day))
            items.Add(new DayItem(r.ReminderDateTime, Reminder: r));
        return[..items.OrderBy(i => i.SortKey)];
    }

    private string GetEmployeeDisplayNameById(string? userId)
    {
        if (string.IsNullOrEmpty(userId))
            return string.Empty;
        var emp = _employees.FirstOrDefault(e => e.Id == userId) ?? _linkedUsers.FirstOrDefault(u => u.Id == userId);
        return emp?.DisplayName ?? emp?.Username ?? userId;
    }

    private List<List<DateOnly>> GetMonthMatrix(DateOnly forDate)
    {
        var result = new List<List<DateOnly>>();
        var firstOfMonth = new DateOnly(forDate.Year, forDate.Month, 1);
        var start = StartOfWeek(firstOfMonth);
        for (int week = 0; week < 6; week++)
        {
            var weekRow = new List<DateOnly>();
            for (int d = 0; d < 7; d++)
                weekRow.Add(start.AddDays(week * 7 + d));
            result.Add(weekRow);
        }

        return result;
    }

    private DateOnly StartOfWeek(DateOnly date)
    {
        var fdow = CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek;
        var diff = ((int)date.DayOfWeek - (int)fdow + 7) % 7;
        return date.AddDays(-diff);
    }
}