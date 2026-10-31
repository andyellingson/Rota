using Rota.Models;

namespace Rota.Services
{
    /// <summary>
    /// Default <see cref="IWorkItemTemplateApplyService"/> backed by <see cref="IWorkItemsService"/>.
    /// </summary>
    public sealed class WorkItemTemplateApplyService : IWorkItemTemplateApplyService
    {
        private readonly IWorkItemsService _workItemsService;
        private readonly IWorkItemTemplatesService? _workItemTemplatesService;

        public WorkItemTemplateApplyService(IWorkItemsService workItemsService)
        {
            _workItemsService = workItemsService;
        }

        public WorkItemTemplateApplyService(IWorkItemsService workItemsService, IWorkItemTemplatesService workItemTemplatesService)
        {
            _workItemsService = workItemsService;
            _workItemTemplatesService = workItemTemplatesService;
        }

        /// <inheritdoc />
        public async Task<WorkItemTemplateApplyResult> ApplyAsync(string workItemTemplateId, string username, string teamCode, string scheduleId, IEnumerable<DateOnly> days)
        {
            if (_workItemTemplatesService is not null)
            {
                var workItemTemplate = await _workItemTemplatesService.GetByIdAsync(workItemTemplateId, teamCode);
                if (workItemTemplate is null || !workItemTemplate.ScheduleIds.Contains(scheduleId, StringComparer.Ordinal))
                {
                    return new WorkItemTemplateApplyResult(0, "The selected Work Item Template is not available for this schedule.");
                }
            }

            var templateWorkItems = await _workItemsService.GetWorkItemTemplateWorkItemsAsync(workItemTemplateId, teamCode);
            if (templateWorkItems.Count == 0)
            {
                return new WorkItemTemplateApplyResult(0, "No template work items found for the selected Work Item Template.");
            }

            // Build the list of work items before persisting so a failure mid-way is easier to reason about.
            var toCreate = new List<WorkItem>();
            foreach (var day in days)
            {
                foreach (var template in templateWorkItems)
                {
                    // Skip templates whose WorkDay does not match the current calendar day.
                    if (template.WorkDay != day.DayOfWeek) continue;

                    // Template work items store wall-clock times; preserve the clock component exactly
                    // and do not convert between time zones when applying to calendar dates.
                    var templateStartTime = TimeOnly.FromDateTime(template.Start);
                    var templateEndTime = TimeOnly.FromDateTime(template.End);
                    var start = day.ToDateTime(templateStartTime, DateTimeKind.Local);
                    var endDay = templateEndTime <= templateStartTime ? day.AddDays(1) : day;
                    var end = endDay.ToDateTime(templateEndTime, DateTimeKind.Local);

                    toCreate.Add(new WorkItem
                    {
                        Username = username,
                        Start = start.ToUniversalTime(),
                        End = end.ToUniversalTime(),
                        Title = template.Title,
                        Color = template.Color,
                        Notes = template.Notes,
                        AssignedToUserId = template.AssignedToUserId,
                        TeamCode = teamCode,
                        ScheduleId = scheduleId,
                        WorkerType = template.WorkerType,
                        RotationId = null,  // Actual calendar WorkItems do not carry a Work Item Template id
                        WorkDay = null      // WorkDay is only meaningful on template WorkItems
                    });
                }
            }

            foreach (var workItem in toCreate)
            {
                await _workItemsService.CreateWorkItemAsync(workItem);
            }

            return new WorkItemTemplateApplyResult(toCreate.Count);
        }

    }
}
