using System.Globalization;
using Rota.Models;

namespace Rota.Components
{
    /// <summary>
    /// View modes supported by the calendar.
    /// </summary>
    public enum ViewMode { Week, Month }

    /// <summary>
    /// Multi-day selection modes supported by the calendar.
    /// </summary>
    public enum SelectionMode { Off, Day, Week }

    /// <summary>
    /// Item types that can be removed through the bulk-delete confirmation modal.
    /// </summary>
    public enum BulkDeleteType { Shifts, Absences, Reminders }

    /// <summary>
    /// A single entry (shift, absence or reminder) displayed in a calendar day cell.
    /// Exactly one of <see cref="Shift"/>, <see cref="Absence"/> or <see cref="Reminder"/> is set.
    /// </summary>
    public sealed record CalendarDayItem(DateTime SortKey, Shift? Shift = null, Absence? Absence = null, Reminder? Reminder = null);

    /// <summary>
    /// Culture-aware date helpers shared by the calendar components.
    /// </summary>
    public static class CalendarDates
    {
        /// <summary>
        /// Returns the first day of the week (per the current culture) containing the given date.
        /// </summary>
        public static DateOnly StartOfWeek(DateOnly date)
        {
            var fdow = CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek;
            var diff = ((int)date.DayOfWeek - (int)fdow + 7) % 7;
            return date.AddDays(-diff);
        }
    }
}
