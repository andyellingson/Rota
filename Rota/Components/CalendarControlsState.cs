using Microsoft.AspNetCore.Components;

namespace Rota.Components;

/// <summary>
/// Scoped (per-circuit) state that lets the Calendar page publish its toolbar controls
/// so the navigation menu can render them beneath the "Schedules" item.
/// </summary>
public sealed class CalendarControlsState
{
    /// <summary>The toolbar content published by the active Calendar page, or null when none.</summary>
    public RenderFragment? Controls { get; private set; }

    /// <summary>Raised when the controls change so subscribers can re-render.</summary>
    public event Action? Changed;

    public void Publish(RenderFragment controls)
    {
        Controls = controls;
        NotifyChanged();
    }

    /// <summary>Clears the controls only if they still belong to the caller.</summary>
    public void Clear(RenderFragment controls)
    {
        if (!ReferenceEquals(Controls, controls))
        {
            return;
        }

        Controls = null;
        NotifyChanged();
    }

    public void NotifyChanged() => Changed?.Invoke();
}
