using Rota.Models;

namespace Rota.Services
{
    /// <summary>
    /// Default <see cref="IRotationTemplateService"/> backed by <see cref="IWorkItemsService"/>.
    /// </summary>
    public sealed class RotationTemplateService : IRotationTemplateService
    {
        private readonly IWorkItemsService _WorkItemsService;
        private readonly IRotationsService? _rotationsService;

        public RotationTemplateService(IWorkItemsService WorkItemsService)
        {
            _WorkItemsService = WorkItemsService;
        }

        public RotationTemplateService(IWorkItemsService WorkItemsService, IRotationsService rotationsService)
        {
            _WorkItemsService = WorkItemsService;
            _rotationsService = rotationsService;
        }

        /// <inheritdoc />
        public async Task<RotationApplyResult> ApplyAsync(string rotationId, string username, string managerCode, string scheduleId, IEnumerable<DateOnly> days)
        {
            if (_rotationsService is not null)
            {
                var rotation = await _rotationsService.GetByIdAsync(rotationId, managerCode);
                if (rotation is null || !rotation.ScheduleIds.Contains(scheduleId, StringComparer.Ordinal))
                {
                    return new RotationApplyResult(0, "The selected rotation is not available for this schedule.");
                }
            }

            var templateWorkItems = await _WorkItemsService.GetRotationTemplateWorkItemsAsync(rotationId, managerCode);
            if (templateWorkItems.Count == 0)
            {
                return new RotationApplyResult(0, "No template work items found for the selected WorkWeek.");
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
                        ManagerCode = managerCode,
                        ScheduleId = scheduleId,
                        WorkerType = template.WorkerType,
                        RotationId = null,  // Actual calendar WorkItems do not carry a rotation id
                        WorkDay = null      // WorkDay is only meaningful on template WorkItems
                    });
                }
            }

            foreach (var WorkItem in toCreate)
            {
                await _WorkItemsService.CreateWorkItemAsync(WorkItem);
            }

            return new RotationApplyResult(toCreate.Count);
        }

    }
}
