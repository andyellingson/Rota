using Rota.Models;

namespace Rota.Services
{
    /// <summary>
    /// Default <see cref="IRotationTemplateService"/> backed by <see cref="IShiftsService"/>.
    /// </summary>
    public sealed class RotationTemplateService : IRotationTemplateService
    {
        private readonly IShiftsService _shiftsService;
        private readonly IRotationsService? _rotationsService;

        public RotationTemplateService(IShiftsService shiftsService)
        {
            _shiftsService = shiftsService;
        }

        public RotationTemplateService(IShiftsService shiftsService, IRotationsService rotationsService)
        {
            _shiftsService = shiftsService;
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

            var templateShifts = await _shiftsService.GetRotationTemplateShiftsAsync(rotationId, managerCode);
            if (templateShifts.Count == 0)
            {
                return new RotationApplyResult(0, "No template shifts found for the selected WorkWeek.");
            }

            // Build the list of shifts before persisting so a failure mid-way is easier to reason about.
            var toCreate = new List<Shift>();
            foreach (var day in days)
            {
                foreach (var template in templateShifts)
                {
                    // Skip templates whose WorkDay does not match the current calendar day.
                    if (template.WorkDay != day.DayOfWeek) continue;

                    var templateStartTime = TimeOnly.FromDateTime(GetTemplateDateTime(template.Start));
                    var templateEndTime = TimeOnly.FromDateTime(GetTemplateDateTime(template.End));
                    var start = day.ToDateTime(templateStartTime, DateTimeKind.Local);
                    var endDay = templateEndTime <= templateStartTime ? day.AddDays(1) : day;
                    var end = endDay.ToDateTime(templateEndTime, DateTimeKind.Local);

                    toCreate.Add(new Shift
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
                        RotationId = null,  // Actual calendar shifts do not carry a rotation id
                        WorkDay = null      // WorkDay is only meaningful on template shifts
                    });
                }
            }

            foreach (var shift in toCreate)
            {
                await _shiftsService.CreateShiftAsync(shift);
            }

            return new RotationApplyResult(toCreate.Count);
        }

        private static DateTime GetTemplateDateTime(DateTime value) => value.Kind switch
        {
            DateTimeKind.Local => value.ToUniversalTime(),
            DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
            _ => value
        };
    }
}
