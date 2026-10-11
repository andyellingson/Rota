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

        /// <summary>Rotation template chosen in the apply-template bar.</summary>
        public string? SelectedRotationId { get; set; }

        /// <summary>Validation or service error from applying a rotation template.</summary>
        public string? RotationError { get; set; }

        /// <summary>True while a rotation template is being applied.</summary>
        public bool IsApplying { get; set; }

        /// <summary>Inclusive start of the range a rotation template is applied to.</summary>
        public DateOnly RotationRangeStart { get; set; }

        /// <summary>Inclusive end of the range a rotation template is applied to.</summary>
        public DateOnly RotationRangeEnd { get; set; }

        /// <summary>Inclusive start of the range shifts are auto-populated for.</summary>
        public DateOnly AutoRangeStart { get; set; }

        /// <summary>Inclusive end of the range shifts are auto-populated for.</summary>
        public DateOnly AutoRangeEnd { get; set; }

        /// <summary>Error message from the last auto-populate run.</summary>
        public string? AutoError { get; set; }

        /// <summary>Warning message from the last auto-populate run.</summary>
        public string? AutoWarning { get; set; }

        /// <summary>Success message from the last auto-populate run.</summary>
        public string? AutoSuccess { get; set; }

        /// <summary>True while auto-populate is running.</summary>
        public bool AutoIsRunning { get; set; }
    }
}
