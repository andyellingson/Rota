using Rota.Models;
using Rota.Services;

namespace Rota.Components
{
    public enum ModalMode
    {
        Reminder,
        Shift,
        Absence
    }

    public enum ShiftRecurrence
    {
        None,
        Daily,
        Workdays,
        Weekends
    }

    public sealed class CalendarEditModalState
    {
        public bool ShowModal { get; set; }
        public DateOnly? SelectedDate { get; set; }
        public ModalMode ModalMode { get; set; } = ModalMode.Reminder;
        public string? ModalError { get; set; }
        public bool IsBusy { get; set; }

        public Reminder? EditingReminder { get; set; }
        public string NewReminderTitle { get; set; } = string.Empty;
        public string? NewReminderNotes { get; set; }
        public TimeOnly NewReminderTime { get; set; } = new(9, 0);
        public string NewReminderColor { get; set; } = "#ffeb3b";
        public string NewReminderForUsername { get; set; } = string.Empty;
        public string EditReminderTitle { get; set; } = string.Empty;
        public string? EditReminderNotes { get; set; }
        public DateOnly EditReminderDate { get; set; }
        public TimeOnly EditReminderTime { get; set; } = new(9, 0);
        public string EditReminderColor { get; set; } = "#ffeb3b";
        public string EditReminderForUsername { get; set; } = string.Empty;

        public Shift? EditingShift { get; set; }
        public string EditShiftId { get; set; } = string.Empty;
        public DateOnly EditShiftDate { get; set; }
        public TimeOnly EditShiftStartTime { get; set; } = new(9, 0);
        public TimeOnly EditShiftEndTime { get; set; } = new(17, 0);
        public string EditShiftTitle { get; set; } = string.Empty;
        public string? EditShiftNotes { get; set; }
        public WorkerType EditShiftWorkerType { get; set; } = WorkerType.General;
        public string EditShiftColor { get; set; } = "#1890ff";
        public string EditShiftAssignedTo { get; set; } = string.Empty;
        public TimeOnly NewShiftStartTime { get; set; } = new(9, 0);
        public TimeOnly NewShiftEndTime { get; set; } = new(17, 0);
        public string NewShiftTitle { get; set; } = string.Empty;
        public string? NewShiftNotes { get; set; }
        public string NewShiftColor { get; set; } = "#1890ff";
        public WorkerType NewShiftWorkerType { get; set; } = WorkerType.General;
        public ShiftRecurrence NewShiftRecurrence { get; set; } = ShiftRecurrence.None;
        public string NewShiftAssignedTo { get; set; } = string.Empty;

        public Absence? EditingAbsence { get; set; }
        public int NewAbsenceDayCount { get; set; } = 1;
        public string NewAbsenceTitle { get; set; } = string.Empty;
        public string? NewAbsenceNotes { get; set; }
        public string NewAbsenceColor { get; set; } = "#fa8c16";
        public string NewAbsenceForUsername { get; set; } = string.Empty;
        public string EditAbsenceStartDateUtc { get; set; } = string.Empty;
        public int EditAbsenceDayCount { get; set; } = 1;
        public string EditAbsenceTitle { get; set; } = string.Empty;
        public string? EditAbsenceNotes { get; set; }
        public string EditAbsenceColor { get; set; } = "#fa8c16";
        public string EditAbsenceForUsername { get; set; } = string.Empty;
    }
}
