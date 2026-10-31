namespace Rota.Components
{
    /// <summary>
    /// Holds the state shared between the calendar page and the day details modal.
    /// </summary>
    public sealed class DayModalState
    {
        /// <summary>Whether the modal is visible.</summary>
        public bool IsOpen { get; set; }

        /// <summary>The day currently shown in the modal.</summary>
        public DateOnly Date { get; set; }
    }
}
