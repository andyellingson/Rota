using Rota.Models;

namespace Rota.Services
{
    /// <summary>
    /// Default <see cref="IWorkItemAutoAssignService"/> backed by <see cref="IWorkItemsService"/>, <see cref="IUserService"/>,
    /// and <see cref="IAbsencesService"/>.
    /// </summary>
    public sealed class WorkItemAutoAssignService : IWorkItemAutoAssignService
    {
        private readonly IWorkItemsService _WorkItemsService;
        private readonly IUserService _userService;
        private readonly IAbsencesService _absencesService;

        public WorkItemAutoAssignService(IWorkItemsService WorkItemsService, IUserService userService, IAbsencesService absencesService)
        {
            _WorkItemsService = WorkItemsService;
            _userService = userService;
            _absencesService = absencesService;
        }

        /// <inheritdoc />
        public async Task<AutoAssignOutcome> AutoAssignAsync(
            string username,
            string? userId,
            string teamCode,
            DateOnly rangeStart,
            DateOnly rangeEnd,
            Func<DateOnly, bool>? includeDay,
            string noWorkItemsMessage)
        {
            // Refresh linked users so we have the latest MaxHours / priority values.
            var linkedUsers = await _userService.GetLinkedUsersForManagerAsync(teamCode);

            // Load work items for the requested range (padded for UTC boundaries).
            var rangeWorkItems = await _WorkItemsService.GetWorkItemsAsync(username, userId, teamCode, rangeStart.AddDays(-1), rangeEnd.AddDays(1));

            var unassigned = rangeWorkItems
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
                return new AutoAssignOutcome(false, noWorkItemsMessage, null, linkedUsers);
            }

            var (weekRangeStart, weekRangeEnd) = GetWeekRange(rangeStart, rangeEnd);

            // Load all work items spanning full Sunday-Saturday weeks so pre-existing hours are counted
            // correctly when enforcing the weekly MaxHours cap.
            var allWeekWorkItems = await LoadFullWeekWorkItemsAsync(username, userId, teamCode, weekRangeStart, weekRangeEnd);

            // Load approved absences for the same assignment span so overlapping requests block assignment.
            var approvedAbsences = await LoadApprovedAbsencesAsync(teamCode, weekRangeStart, weekRangeEnd);

            var result = await ExecuteAsync(username, linkedUsers, unassigned, allWeekWorkItems, approvedAbsences);
            var (success, warning) = BuildMessages(result, linkedUsers);

            return new AutoAssignOutcome(result.AssignedCount > 0, success, warning, linkedUsers);
        }

        /// <summary>
        /// Loads all work items spanning the supplied Sunday-Saturday week boundaries.
        /// </summary>
        private Task<List<WorkItem>> LoadFullWeekWorkItemsAsync(string username, string? userId, string teamCode, DateOnly weekRangeStart, DateOnly weekRangeEnd)
            => _WorkItemsService.GetWorkItemsAsync(username, userId, teamCode, weekRangeStart.AddDays(-1), weekRangeEnd.AddDays(1));

        /// <summary>
        /// Loads only approved absences for users under the manager scope.
        /// </summary>
        private async Task<List<Absence>> LoadApprovedAbsencesAsync(string teamCode, DateOnly weekRangeStart, DateOnly weekRangeEnd)
        {
            var absences = await _absencesService.GetAbsencesAsync(teamCode, null, weekRangeStart, weekRangeEnd) ?? [];
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
                List<WorkItem> unassigned,
                List<WorkItem> allWeekWorkItems,
                List<Absence> approvedAbsences)
        {
            // Build lookup: team-member-type -> eligible roster members (including the logged-in manager)
            // so manager work items can also be auto-filled when manager constraints allow it.
            var eligibleByTeamMemberType = linkedUsers
                .GroupBy(u => string.IsNullOrWhiteSpace(u.TeamMemberType) ? "General" : u.TeamMemberType, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

            var approvedAbsencesByUser = approvedAbsences
                .Where(a => !string.IsNullOrWhiteSpace(a.AssignedToUserId) || !string.IsNullOrWhiteSpace(a.UserId))
                .GroupBy(a => a.AssignedToUserId ?? a.UserId!, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

            // Seed weekly hours from already-assigned work items so existing workload is respected.
            // Key: employeeId -> (weekKey -> hoursAssigned)
            var weeklyHours = new Dictionary<string, Dictionary<string, double>>(StringComparer.OrdinalIgnoreCase);

            foreach (var s in allWeekWorkItems.Where(s => !string.IsNullOrEmpty(s.AssignedToUserId)))
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
            // one work item to the same person on a single day.
            var assignedDaysByUser = new Dictionary<string, HashSet<DateOnly>>(StringComparer.OrdinalIgnoreCase);
            foreach (var s in allWeekWorkItems.Where(s => !string.IsNullOrEmpty(s.AssignedToUserId)))
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

            // Assign in day + team-member-type batches so when multiple work items exist for the same
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
                    .OrderByDescending(c => c.WorkItemAssignmentPriority)
                    .ThenBy(c => c.DisplayName ?? c.Username ?? c.Id)
                    .ToList();

                foreach (var WorkItem in group.OrderBy(s => s.Start))
                {
                    var WorkItemHours = (WorkItem.End - WorkItem.Start).TotalHours;
                    var weekKey = GetWeekKey(WorkItem.Start.ToLocalTime());
                    var WorkItemDay = group.Key.Day;

                    string? chosenId = null;

                    foreach (var candidate in orderedCandidates)
                    {
                        if (candidate.Id is null)
                        {
                            continue;
                        }

                        if (assignedDaysByUser.TryGetValue(candidate.Id, out var assignedDays) && assignedDays.Contains(WorkItemDay))
                        {
                            continue;
                        }

                        // Employee availability must fully contain the work item for that day.
                        if (!IsWorkItemWithinCandidateAvailability(candidate, WorkItem.Start, WorkItem.End))
                        {
                            continue;
                        }

                        // Approved absences block any overlapping assignment windows.
                        if (HasApprovedAbsenceConflict(candidate.Id, WorkItem.Start, WorkItem.End, approvedAbsencesByUser))
                        {
                            continue;
                        }

                        // Enforce MaxHours cap per Sunday-Saturday calendar week.
                        if (candidate.MaxHours > 0)
                        {
                            weeklyHours.TryGetValue(candidate.Id, out var empMap);
                            double hoursUsed = 0;
                            empMap?.TryGetValue(weekKey, out hoursUsed);
                            if (hoursUsed + WorkItemHours > candidate.MaxHours)
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

                    await _WorkItemsService.UpdateWorkItemAsync(WorkItem.Id!, username, WorkItem.Start, WorkItem.End,
                        WorkItem.Title, WorkItem.Notes, WorkItem.WorkerType, WorkItem.Color, chosenId);

                    // Immediately update in-memory weekly hours so the next iteration in the same
                    // week sees the hours just committed.
                    if (!weeklyHours.TryGetValue(chosenId, out var chosenWeeks))
                    {
                        chosenWeeks = new Dictionary<string, double>();
                        weeklyHours[chosenId] = chosenWeeks;
                    }
                    chosenWeeks.TryGetValue(weekKey, out var currentHours);
                    chosenWeeks[weekKey] = currentHours + WorkItemHours;

                    if (!assignedDaysByUser.TryGetValue(chosenId, out var chosenDays))
                    {
                        chosenDays = new HashSet<DateOnly>();
                        assignedDaysByUser[chosenId] = chosenDays;
                    }
                    chosenDays.Add(WorkItemDay);

                    assignedThisRun.TryGetValue(chosenId, out var runPrev);
                    assignedThisRun[chosenId] = (runPrev.Count + 1, runPrev.Hours + WorkItemHours);
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
                return ("No work items could be assigned. Check that employees with matching team member types exist and have sufficient weekly hours remaining.", null);
            }

            var breakdown = result.AssignedThisRun
                .Select(kvp =>
                {
                    var emp = linkedUsers.FirstOrDefault(u => u.Id == kvp.Key);
                    var name = emp?.DisplayName ?? emp?.Username ?? kvp.Key;
                    return $"{name}: {kvp.Value.Count} work item{(kvp.Value.Count == 1 ? "" : "s")} ({kvp.Value.Hours:0.#}h assigned)";
                })
                .ToList();

            var success = $"Successfully assigned {result.AssignedCount} work item{(result.AssignedCount == 1 ? "" : "s")}. {string.Join(", ", breakdown)}";

            string? warning = null;
            if (result.UncappedIds.Count > 0)
            {
                var names = result.UncappedIds
                    .Select(id => linkedUsers.FirstOrDefault(u => u.Id == id))
                    .Where(u => u is not null)
                    .Select(u => u!.DisplayName ?? u.Username)
                    .ToList();
                warning = $"⚠️ The following employee(s) have no weekly hour limit set (Max Hours = 0) and were assigned work items without a cap: {string.Join(", ", names)}. " +
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
        /// Returns whether the candidate has an availability window that fully contains the work item.
        /// </summary>
        private static bool IsWorkItemWithinCandidateAvailability(User candidate, DateTime WorkItemstartUtc, DateTime WorkItemEndUtc)
        {
            var localWorkItemstart = WorkItemstartUtc.ToLocalTime();
            var localWorkItemEnd = WorkItemEndUtc.ToLocalTime();
            var WorkItemDay = localWorkItemstart.DayOfWeek;

            var windowsForDay = candidate.Availability
                .Where(a => a.DayOfWeek == WorkItemDay)
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

                var availabilityStart = localWorkItemstart.Date.Add(availabilityStartTime.ToTimeSpan());
                var availabilityEnd = localWorkItemstart.Date.Add(availabilityEndTime.ToTimeSpan());

                // Support overnight availability windows such as 22:00-06:00.
                if (availabilityEnd <= availabilityStart)
                {
                    availabilityEnd = availabilityEnd.AddDays(1);
                }

                if (localWorkItemstart >= availabilityStart && localWorkItemEnd <= availabilityEnd)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Returns true when the work item overlaps any approved absence for the user.
        /// </summary>
        private static bool HasApprovedAbsenceConflict(
            string candidateId,
            DateTime WorkItemstartUtc,
            DateTime WorkItemEndUtc,
            Dictionary<string, List<Absence>> approvedAbsencesByUser)
        {
            if (!approvedAbsencesByUser.TryGetValue(candidateId, out var absences) || absences.Count == 0)
            {
                return false;
            }

            foreach (var absence in absences)
            {
                if (WorkItemstartUtc < absence.EndDate && WorkItemEndUtc > absence.StartDate)
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
