using Rota.Models;

namespace Rota.Services
{
    /// <summary>
    /// Default <see cref="IShiftAutoAssignService"/> backed by <see cref="IShiftsService"/>, <see cref="IUserService"/>,
    /// and <see cref="IAbsencesService"/>.
    /// </summary>
    public sealed class ShiftAutoAssignService : IShiftAutoAssignService
    {
        private readonly IShiftsService _shiftsService;
        private readonly IUserService _userService;
        private readonly IAbsencesService _absencesService;

        public ShiftAutoAssignService(IShiftsService shiftsService, IUserService userService, IAbsencesService absencesService)
        {
            _shiftsService = shiftsService;
            _userService = userService;
            _absencesService = absencesService;
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

            var (weekRangeStart, weekRangeEnd) = GetWeekRange(rangeStart, rangeEnd);

            // Load all shifts spanning full Sunday-Saturday weeks so pre-existing hours are counted
            // correctly when enforcing the weekly MaxHours cap.
            var allWeekShifts = await LoadFullWeekShiftsAsync(username, userId, managerCode, weekRangeStart, weekRangeEnd);

            // Load approved absences for the same assignment span so overlapping requests block assignment.
            var approvedAbsences = await LoadApprovedAbsencesAsync(managerCode, weekRangeStart, weekRangeEnd);

            var result = await ExecuteAsync(username, linkedUsers, unassigned, allWeekShifts, approvedAbsences);
            var (success, warning) = BuildMessages(result, linkedUsers);

            return new AutoAssignOutcome(result.AssignedCount > 0, success, warning, linkedUsers);
        }

        /// <summary>
        /// Loads all shifts spanning the supplied Sunday-Saturday week boundaries.
        /// </summary>
        private Task<List<Shift>> LoadFullWeekShiftsAsync(string username, string? userId, string managerCode, DateOnly weekRangeStart, DateOnly weekRangeEnd)
            => _shiftsService.GetShiftsAsync(username, userId, managerCode, weekRangeStart.AddDays(-1), weekRangeEnd.AddDays(1));

        /// <summary>
        /// Loads only approved absences for users under the manager scope.
        /// </summary>
        private async Task<List<Absence>> LoadApprovedAbsencesAsync(string managerCode, DateOnly weekRangeStart, DateOnly weekRangeEnd)
        {
            var absences = await _absencesService.GetAbsencesAsync(managerCode, null, weekRangeStart, weekRangeEnd) ?? [];
            return absences.Where(a => a.ApprovalState == AbsenceApprovalState.Approved).ToList();
        }

        /// <summary>
        /// Core priority-aware assignment engine enforcing availability windows, approved-absence overlap,
        /// and MaxHours by Sunday-Saturday week.
        /// </summary>
        private async Task<(int AssignedCount, Dictionary<string, (int Count, double Hours)> AssignedThisRun, HashSet<string> UncappedIds)>
            ExecuteAsync(
                string username,
                List<User> linkedUsers,
                List<Shift> unassigned,
                List<Shift> allWeekShifts,
                List<Absence> approvedAbsences)
        {
            // Build lookup: team-member-type -> eligible roster members (including the logged-in manager)
            // so manager shifts can also be auto-filled when manager constraints allow it.
            var eligibleByTeamMemberType = linkedUsers
                .GroupBy(u => string.IsNullOrWhiteSpace(u.TeamMemberType) ? "General" : u.TeamMemberType, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

            var approvedAbsencesByUser = approvedAbsences
                .Where(a => !string.IsNullOrWhiteSpace(a.AssignedToUserId) || !string.IsNullOrWhiteSpace(a.UserId))
                .GroupBy(a => a.AssignedToUserId ?? a.UserId!, StringComparer.OrdinalIgnoreCase)
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

            // Assign in day + team-member-type batches so when multiple shifts exist for the same
            // team member type on one day, they are distributed by descending priority.
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
                if (!eligibleByTeamMemberType.TryGetValue(group.Key.WorkerType, out var candidates) || candidates.Count == 0)
                {
                    continue;
                }

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
                        if (candidate.Id is null)
                        {
                            continue;
                        }

                        if (assignedDaysByUser.TryGetValue(candidate.Id, out var assignedDays) && assignedDays.Contains(shiftDay))
                        {
                            continue;
                        }

                        // Employee availability must fully contain the shift for that day.
                        if (!IsShiftWithinCandidateAvailability(candidate, shift.Start, shift.End))
                        {
                            continue;
                        }

                        // Approved absences block any overlapping assignment windows.
                        if (HasApprovedAbsenceConflict(candidate.Id, shift.Start, shift.End, approvedAbsencesByUser))
                        {
                            continue;
                        }

                        // Enforce MaxHours cap per Sunday-Saturday calendar week.
                        if (candidate.MaxHours > 0)
                        {
                            weeklyHours.TryGetValue(candidate.Id, out var empMap);
                            double hoursUsed = 0;
                            empMap?.TryGetValue(weekKey, out hoursUsed);
                            if (hoursUsed + shiftHours > candidate.MaxHours)
                            {
                                continue;
                            }
                        }

                        chosenId = candidate.Id;
                        if (candidate.MaxHours <= 0)
                        {
                            uncappedIds.Add(candidate.Id);
                        }
                        break;
                    }

                    if (chosenId is null)
                    {
                        continue;
                    }

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
                return ("No shifts could be assigned. Check that employees with matching team member types exist and have sufficient weekly hours remaining.", null);
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
        /// Calculates the Sunday-Saturday week span that fully contains the requested date range.
        /// </summary>
        private static (DateOnly WeekStart, DateOnly WeekEnd) GetWeekRange(DateOnly rangeStart, DateOnly rangeEnd)
        {
            var startDiff = (int)rangeStart.DayOfWeek;
            var endDiff = DayOfWeek.Saturday - rangeEnd.DayOfWeek;
            return (rangeStart.AddDays(-startDiff), rangeEnd.AddDays(endDiff));
        }

        /// <summary>
        /// Returns whether the candidate has an availability window that fully contains the shift.
        /// </summary>
        private static bool IsShiftWithinCandidateAvailability(User candidate, DateTime shiftStartUtc, DateTime shiftEndUtc)
        {
            var localShiftStart = shiftStartUtc.ToLocalTime();
            var localShiftEnd = shiftEndUtc.ToLocalTime();
            var shiftDay = localShiftStart.DayOfWeek;

            var windowsForDay = candidate.Availability
                .Where(a => a.DayOfWeek == shiftDay)
                .ToList();

            if (windowsForDay.Count == 0)
            {
                return false;
            }

            foreach (var window in windowsForDay)
            {
                if (!TimeOnly.TryParse(window.StartTime, out var availabilityStartTime)
                    || !TimeOnly.TryParse(window.EndTime, out var availabilityEndTime))
                {
                    continue;
                }

                var availabilityStart = localShiftStart.Date.Add(availabilityStartTime.ToTimeSpan());
                var availabilityEnd = localShiftStart.Date.Add(availabilityEndTime.ToTimeSpan());

                // Support overnight availability windows such as 22:00-06:00.
                if (availabilityEnd <= availabilityStart)
                {
                    availabilityEnd = availabilityEnd.AddDays(1);
                }

                if (localShiftStart >= availabilityStart && localShiftEnd <= availabilityEnd)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Returns true when the shift overlaps any approved absence for the user.
        /// </summary>
        private static bool HasApprovedAbsenceConflict(
            string candidateId,
            DateTime shiftStartUtc,
            DateTime shiftEndUtc,
            Dictionary<string, List<Absence>> approvedAbsencesByUser)
        {
            if (!approvedAbsencesByUser.TryGetValue(candidateId, out var absences) || absences.Count == 0)
            {
                return false;
            }

            foreach (var absence in absences)
            {
                if (shiftStartUtc < absence.EndDate && shiftEndUtc > absence.StartDate)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Returns a key identifying the Sunday that starts the week containing the local date/time.
        /// </summary>
        private static string GetWeekKey(DateTime localDateTime)
        {
            var diff = (int)localDateTime.DayOfWeek;
            return localDateTime.Date.AddDays(-diff).ToString("yyyy-MM-dd");
        }
    }
}
