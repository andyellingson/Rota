using Rota.Models;

namespace Rota.Components
{
    /// <summary>
    /// Identifies which editor mode is currently active in the calendar modal.
    /// </summary>
    public enum ModalMode
    {
        Reminder,
        Shift,
        Absence
    }

    /// <summary>
    /// Defines recurrence options available when creating a new shift.
    /// </summary>
    public enum ShiftRecurrence
    {
        None,
        Daily,
        Workdays,
        Weekends
    }

    /// <summary>
    /// Holds mutable UI state for the calendar edit modal.
    /// </summary>
    public sealed class CalendarEditModalState
    {
        /// <summary>
        /// Gets or sets a value indicating whether the modal is visible.
        /// </summary>
        public bool ShowModal { get; set; }

        /// <summary>
        /// Gets or sets the calendar date currently selected in the UI.
        /// </summary>
        public DateOnly? SelectedDate { get; set; }

        /// <summary>
        /// Gets or sets the active modal mode.
        /// </summary>
        public ModalMode ModalMode { get; set; } = ModalMode.Reminder;

        /// <summary>
        /// Gets or sets the error message displayed inside the modal.
        /// </summary>
        public string? ModalError { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the modal is executing a save/delete operation.
        /// </summary>
        public bool IsBusy { get; set; }

        /// <summary>
        /// Gets or sets the reminder currently being edited.
        /// </summary>
        public Reminder? EditingReminder { get; set; }

        /// <summary>
        /// Gets or sets the title used when creating a new reminder.
        /// </summary>
        public string NewReminderTitle { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets notes used when creating a new reminder.
        /// </summary>
        public string? NewReminderNotes { get; set; }

        /// <summary>
        /// Gets or sets the reminder time used for new reminders.
        /// </summary>
        public TimeOnly NewReminderTime { get; set; } = new(9, 0);

        /// <summary>
        /// Gets or sets the reminder color used for new reminders.
        /// </summary>
        public string NewReminderColor { get; set; } = "#ffeb3b";

        /// <summary>
        /// Gets or sets the target username for a new reminder.
        /// </summary>
        public string NewReminderForUsername { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the title for the reminder being edited.
        /// </summary>
        public string EditReminderTitle { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets notes for the reminder being edited.
        /// </summary>
        public string? EditReminderNotes { get; set; }

        /// <summary>
        /// Gets or sets the date for the reminder being edited.
        /// </summary>
        public DateOnly EditReminderDate { get; set; }

        /// <summary>
        /// Gets or sets the time for the reminder being edited.
        /// </summary>
        public TimeOnly EditReminderTime { get; set; } = new(9, 0);

        /// <summary>
        /// Gets or sets the color for the reminder being edited.
        /// </summary>
        public string EditReminderColor { get; set; } = "#ffeb3b";

        /// <summary>
        /// Gets or sets the target username for the reminder being edited.
        /// </summary>
        public string EditReminderForUsername { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the shift currently being edited.
        /// </summary>
        public Shift? EditingShift { get; set; }

        /// <summary>
        /// Gets or sets the identifier of the shift currently being edited.
        /// </summary>
        public string EditShiftId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the date for the shift being edited.
        /// </summary>
        public DateOnly EditShiftDate { get; set; }

        /// <summary>
        /// Gets or sets the start time for the shift being edited.
        /// </summary>
        public TimeOnly EditShiftStartTime { get; set; } = new(9, 0);

        /// <summary>
        /// Gets or sets the end time for the shift being edited.
        /// </summary>
        public TimeOnly EditShiftEndTime { get; set; } = new(17, 0);

        /// <summary>
        /// Gets or sets the title for the shift being edited.
        /// </summary>
        public string EditShiftTitle { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets notes for the shift being edited.
        /// </summary>
        public string? EditShiftNotes { get; set; }

        /// <summary>
        /// Gets or sets the worker type name for the shift being edited.
        /// </summary>
        public string EditShiftWorkerType { get; set; } = "General";

        /// <summary>
        /// Gets or sets the color for the shift being edited.
        /// </summary>
        public string EditShiftColor { get; set; } = "#1890ff";

        /// <summary>
        /// Gets or sets the assigned user id for the shift being edited.
        /// </summary>
        public string EditShiftAssignedTo { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the start time for a new shift.
        /// </summary>
        public TimeOnly NewShiftStartTime { get; set; } = new(9, 0);

        /// <summary>
        /// Gets or sets the end time for a new shift.
        /// </summary>
        public TimeOnly NewShiftEndTime { get; set; } = new(17, 0);

        /// <summary>
        /// Gets or sets the title for a new shift.
        /// </summary>
        public string NewShiftTitle { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets notes for a new shift.
        /// </summary>
        public string? NewShiftNotes { get; set; }

        /// <summary>
        /// Gets or sets the color for a new shift.
        /// </summary>
        public string NewShiftColor { get; set; } = "#1890ff";

        /// <summary>
        /// Gets or sets the worker type name for the shift being created.
        /// </summary>
        public string NewShiftWorkerType { get; set; } = "General";

        /// <summary>
        /// Gets or sets the recurrence mode for newly created shifts.
        /// </summary>
        public ShiftRecurrence NewShiftRecurrence { get; set; } = ShiftRecurrence.None;

        /// <summary>
        /// Gets or sets the assigned user id for a new shift.
        /// </summary>
        public string NewShiftAssignedTo { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the absence currently being edited.
        /// </summary>
        public Absence? EditingAbsence { get; set; }

        /// <summary>
        /// Gets or sets day count for newly created absences.
        /// </summary>
        public int NewAbsenceDayCount { get; set; } = 1;

        /// <summary>
        /// Gets or sets title for newly created absences.
        /// </summary>
        public string NewAbsenceTitle { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets notes for newly created absences.
        /// </summary>
        public string? NewAbsenceNotes { get; set; }

        /// <summary>
        /// Gets or sets color for newly created absences.
        /// </summary>
        public string NewAbsenceColor { get; set; } = "#fa8c16";

        /// <summary>
        /// Gets or sets the target username for newly created absences.
        /// </summary>
        public string NewAbsenceForUsername { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether the new absence is part-day.
        /// </summary>
        public bool NewAbsenceIsPartDay { get; set; }

        /// <summary>
        /// Gets or sets the part-day start time for newly created absences.
        /// </summary>
        public TimeOnly NewAbsenceStartTime { get; set; } = new(9, 0);

        /// <summary>
        /// Gets or sets the part-day end time for newly created absences.
        /// </summary>
        public TimeOnly NewAbsenceEndTime { get; set; } = new(17, 0);

        /// <summary>
        /// Gets or sets the UTC start-date string for the absence being edited.
        /// </summary>
        public string EditAbsenceStartDateUtc { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets day count for the absence being edited.
        /// </summary>
        public int EditAbsenceDayCount { get; set; } = 1;

        /// <summary>
        /// Gets or sets title for the absence being edited.
        /// </summary>
        public string EditAbsenceTitle { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets notes for the absence being edited.
        /// </summary>
        public string? EditAbsenceNotes { get; set; }

        /// <summary>
        /// Gets or sets color for the absence being edited.
        /// </summary>
        public string EditAbsenceColor { get; set; } = "#fa8c16";

        /// <summary>
        /// Gets or sets target username for the absence being edited.
        /// </summary>
        public string EditAbsenceForUsername { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether the edited absence is part-day.
        /// </summary>
        public bool EditAbsenceIsPartDay { get; set; }

        /// <summary>
        /// Gets or sets part-day start time for the absence being edited.
        /// </summary>
        public TimeOnly EditAbsenceStartTime { get; set; } = new(9, 0);

        /// <summary>
        /// Gets or sets part-day end time for the absence being edited.
        /// </summary>
        public TimeOnly EditAbsenceEndTime { get; set; } = new(17, 0);

        /// <summary>
        /// Gets or sets approval state for newly created absences.
        /// </summary>
        public AbsenceApprovalState NewAbsenceApprovalState { get; set; } = AbsenceApprovalState.Pending;

        /// <summary>
        /// Gets or sets approval state for absences being edited.
        /// </summary>
        public AbsenceApprovalState EditAbsenceApprovalState { get; set; } = AbsenceApprovalState.Pending;
    }
}
