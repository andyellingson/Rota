using System.Globalization;
using Rota.Models;

namespace Rota.Services
{
    /// <summary>
    /// Default <see cref="IShiftAutoAssignService"/> backed by <see cref="IShiftsService"/> and <see cref="IUserService"/>.
    /// </summary>
    public sealed class ShiftAutoAssignService : IShiftAutoAssignService
    {
        private readonly IShiftsService _shiftsService;
        private readonly IUserService _userService;

        public ShiftAutoAssignService(IShiftsService shiftsService, IUserService userService)
        {
            _shiftsService = shiftsService;
            _userService = userService;
        }

        /// <inheritdoc />
        public async Task<AutoAssignOutcome> AutoAssignAsync(
            string username,
            string? userId,
            string managerCode,
            DateOnly rangeStart,
            DateOnly rangeEnd,
            Func<DateOnly, bool>? includeDay,
            string noShiftsMessage)
        {
            // Refresh linked users so we have the latest MaxHours / priority values.
            var linkedUsers = await _userService.GetLinkedUsersForManagerAsync(managerCode);

            // Load shifts for the requested range (padded for UTC boundaries).
            var rangeShifts = await _shiftsService.GetShiftsAsync(username, userId, managerCode, rangeStart.AddDays(-1), rangeEnd.AddDays(1));

            var unassigned = rangeShifts
                .Where(s => string.IsNullOrEmpty(s.AssignedToUserId))
                .Where(s =>
                {
                    var d = DateOnly.FromDateTime(s.Start.ToLocalTime());
                    return d >= rangeStart && d <= rangeEnd && (includeDay is null || includeDay(d));
                })
                .OrderBy(s => s.Start)
                .ToList();

            if (unassigned.Count == 0)
            {
                return new AutoAssignOutcome(false, noShiftsMessage, null, linkedUsers);
            }

            // Load all shifts spanning the full calendar weeks so pre-existing hours
            // are counted correctly when enforcing the weekly MaxHours cap.
            var allWeekShifts = await LoadFullWeekShiftsAsync(username, userId, managerCode, rangeStart, rangeEnd);

            var result = await ExecuteAsync(username, userId, linkedUsers, unassigned, allWeekShifts);
            var (success, warning) = BuildMessages(result, linkedUsers);

            return new AutoAssignOutcome(result.AssignedCount > 0, success, warning, linkedUsers);
        }

        /// <summary>
        /// Loads all shifts spanning the full calendar-week boundaries that overlap the given range.
        /// </summary>
        private Task<List<Shift>> LoadFullWeekShiftsAsync(string username, string? userId, string managerCode, DateOnly rangeStart, DateOnly rangeEnd)
        {
            var fdow = (int)CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek;
            var startDiff = ((int)rangeStart.DayOfWeek - fdow + 7) % 7;
            var endDiff = (fdow + 6 - (int)rangeEnd.DayOfWeek + 7) % 7;
            var weekStart = rangeStart.AddDays(-startDiff).AddDays(-1); // pad one day for UTC
            var weekEnd = rangeEnd.AddDays(endDiff).AddDays(1);         // pad one day for UTC
            return _shiftsService.GetShiftsAsync(username, userId, managerCode, weekStart, weekEnd);
        }

        /// <summary>
        /// Core priority-aware, MaxHours-respecting assignment engine.
        /// </summary>
        private async Task<(int AssignedCount, Dictionary<string, (int Count, double Hours)> AssignedThisRun, HashSet<string> UncappedIds)>
            ExecuteAsync(string username, string? userId, List<User> linkedUsers, List<Shift> unassigned, List<Shift> allWeekShifts)
        {
            // Build lookup: occupation -> eligible employees (manager excluded).
            var eligibleByOccupation = linkedUsers
                .Where(u => u.Id != userId)
                .GroupBy(u => string.IsNullOrWhiteSpace(u.Occupation) ? "General" : u.Occupation, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

            // Seed weekly hours from already-assigned shifts so existing workload is respected.
            // Key: employeeId -> (weekKey -> hoursAssigned)
            var weeklyHours = new Dictionary<string, Dictionary<string, double>>(StringComparer.OrdinalIgnoreCase);

            foreach (var s in allWeekShifts.Where(s => !string.IsNullOrEmpty(s.AssignedToUserId)))
            {
                var wk = GetWeekKey(s.Start.ToLocalTime());
                if (!weeklyHours.TryGetValue(s.AssignedToUserId!, out var empMap))
                {
                    empMap = new Dictionary<string, double>();
                    weeklyHours[s.AssignedToUserId!] = empMap;
                }
                empMap.TryGetValue(wk, out var prev);
                empMap[wk] = prev + (s.End - s.Start).TotalHours;
            }

            // Track days already assigned per employee so auto-assign never gives more than
            // one shift to the same person on a single day.
            var assignedDaysByUser = new Dictionary<string, HashSet<DateOnly>>(StringComparer.OrdinalIgnoreCase);
            foreach (var s in allWeekShifts.Where(s => !string.IsNullOrEmpty(s.AssignedToUserId)))
            {
                var day = DateOnly.FromDateTime(s.Start.ToLocalTime());
                if (!assignedDaysByUser.TryGetValue(s.AssignedToUserId!, out var daySet))
                {
                    daySet = new HashSet<DateOnly>();
                    assignedDaysByUser[s.AssignedToUserId!] = daySet;
                }
                daySet.Add(day);
            }

            var assignedThisRun = new Dictionary<string, (int Count, double Hours)>(StringComparer.OrdinalIgnoreCase);
            var uncappedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int assignedCount = 0;

            // Assign in day + worker-type batches so when multiple shifts exist for the same
            // worker type on one day, they are distributed by descending priority.
            var groupedUnassigned = unassigned
                .GroupBy(s => new
                {
                    Day = DateOnly.FromDateTime(s.Start.ToLocalTime()),
                    WorkerType = string.IsNullOrWhiteSpace(s.WorkerType) ? "General" : s.WorkerType
                })
                .OrderBy(g => g.Key.Day)
                .ThenBy(g => g.Min(s => s.Start))
                .ToList();

            foreach (var group in groupedUnassigned)
            {
                if (!eligibleByOccupation.TryGetValue(group.Key.WorkerType, out var candidates) || candidates.Count == 0)
                    continue;

                var orderedCandidates = candidates
                    .OrderByDescending(c => c.ShiftAssignmentPriority)
                    .ThenBy(c => c.DisplayName ?? c.Username ?? c.Id)
                    .ToList();

                foreach (var shift in group.OrderBy(s => s.Start))
                {
                    var shiftHours = (shift.End - shift.Start).TotalHours;
                    var weekKey = GetWeekKey(shift.Start.ToLocalTime());
                    var shiftDay = group.Key.Day;

                    string? chosenId = null;

                    foreach (var candidate in orderedCandidates)
                    {
                        if (candidate.Id is null) continue;

                        if (assignedDaysByUser.TryGetValue(candidate.Id, out var assignedDays) && assignedDays.Contains(shiftDay))
                            continue;

                        // Enforce MaxHours cap per calendar week.
                        if (candidate.MaxHours > 0)
                        {
                            weeklyHours.TryGetValue(candidate.Id, out var empMap);
                            double hoursUsed = 0;
                            empMap?.TryGetValue(weekKey, out hoursUsed);
                            if (hoursUsed + shiftHours > candidate.MaxHours)
                                continue;
                        }

                        chosenId = candidate.Id;
                        if (candidate.MaxHours <= 0) uncappedIds.Add(candidate.Id);
                        break;
                    }

                    if (chosenId is null) continue;

                    await _shiftsService.UpdateShiftAsync(shift.Id!, username, shift.Start, shift.End,
                        shift.Title, shift.Notes, shift.WorkerType, shift.Color, chosenId);

                    // Immediately update in-memory weekly hours so the next iteration in the same
                    // week sees the hours just committed.
                    if (!weeklyHours.TryGetValue(chosenId, out var chosenWeeks))
                    {
                        chosenWeeks = new Dictionary<string, double>();
                        weeklyHours[chosenId] = chosenWeeks;
                    }
                    chosenWeeks.TryGetValue(weekKey, out var currentHours);
                    chosenWeeks[weekKey] = currentHours + shiftHours;

                    if (!assignedDaysByUser.TryGetValue(chosenId, out var chosenDays))
                    {
                        chosenDays = new HashSet<DateOnly>();
                        assignedDaysByUser[chosenId] = chosenDays;
                    }
                    chosenDays.Add(shiftDay);

                    assignedThisRun.TryGetValue(chosenId, out var runPrev);
                    assignedThisRun[chosenId] = (runPrev.Count + 1, runPrev.Hours + shiftHours);
                    assignedCount++;
                }
            }

            return (assignedCount, assignedThisRun, uncappedIds);
        }

        /// <summary>
        /// Converts an assignment result into human-readable success and warning messages.
        /// </summary>
        private static (string? Success, string? Warning) BuildMessages(
            (int AssignedCount, Dictionary<string, (int Count, double Hours)> AssignedThisRun, HashSet<string> UncappedIds) result,
            List<User> linkedUsers)
        {
            if (result.AssignedCount == 0)
            {
                return ("No shifts could be assigned. Check that employees with matching occupations exist and have sufficient weekly hours remaining.", null);
            }

            var breakdown = result.AssignedThisRun
                .Select(kvp =>
                {
                    var emp = linkedUsers.FirstOrDefault(u => u.Id == kvp.Key);
                    var name = emp?.DisplayName ?? emp?.Username ?? kvp.Key;
                    return $"{name}: {kvp.Value.Count} shift{(kvp.Value.Count == 1 ? "" : "s")} ({kvp.Value.Hours:0.#}h assigned)";
                })
                .ToList();

            var success = $"Successfully assigned {result.AssignedCount} shift{(result.AssignedCount == 1 ? "" : "s")}. {string.Join(", ", breakdown)}";

            string? warning = null;
            if (result.UncappedIds.Count > 0)
            {
                var names = result.UncappedIds
                    .Select(id => linkedUsers.FirstOrDefault(u => u.Id == id))
                    .Where(u => u is not null)
                    .Select(u => u!.DisplayName ?? u.Username)
                    .ToList();
                warning = $"⚠️ The following employee(s) have no weekly hour limit set (Max Hours = 0) and were assigned shifts without a cap: {string.Join(", ", names)}. " +
                    "Set their Max Hours in the Manage page to enforce a weekly limit.";
            }

            return (success, warning);
        }

        /// <summary>
        /// Returns a key identifying the culture-week containing the given local date/time: the date
        /// of the week's first day (per the current culture) formatted as "yyyy-MM-dd". Using the
        /// culture's first day of week keeps the hour cap aligned with how the calendar renders weeks.
        /// </summary>
        private static string GetWeekKey(DateTime localDateTime)
        {
            var fdow = (int)CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek;
            var diff = ((int)localDateTime.DayOfWeek - fdow + 7) % 7;
            return localDateTime.AddDays(-diff).ToString("yyyy-MM-dd");
        }
    }
}
