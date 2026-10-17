using System.Globalization;

namespace CrowdSim.OpenGL.UI;

/// <summary>
/// Small state holder for ImGui text-based numeric fields. Unlike DragFloat/InputFloat,
/// a normal InputText control keeps a visible insertion caret on a single click and does
/// not select the complete value unless the user explicitly selects it.
/// </summary>
internal sealed class CaretNumberEditor
{
    private readonly bool _integer;

    public CaretNumberEditor(float initialValue, bool integer = false)
    {
        _integer = integer;
        Reset(initialValue);
    }

    /// <summary>Current editable text buffer passed by reference to ImGui.InputText.</summary>
    public string Text = string.Empty;

    /// <summary>Whether the ImGui input was active during the previous frame.</summary>
    public bool WasActive { get; set; }

    /// <summary>Validation message retained while the user corrects an invalid value.</summary>
    public string? Error { get; set; }

    /// <summary>Last value copied from the simulation configuration.</summary>
    public float LastCommittedValue { get; private set; }

    /// <summary>
    /// Synchronise external configuration changes without overwriting text while a user is
    /// actively editing or correcting an invalid entry.
    /// </summary>
    public void Synchronize(float currentValue)
    {
        if (WasActive || Error is not null)
        {
            return;
        }

        if (MathF.Abs(currentValue - LastCommittedValue) > 0.0001f)
        {
            Reset(currentValue);
        }
    }

    /// <summary>Replace the buffer with a validated committed value.</summary>
    public void Reset(float value)
    {
        LastCommittedValue = value;
        Text = _integer
            ? MathF.Round(value).ToString("0", CultureInfo.InvariantCulture)
            : value.ToString("0.##", CultureInfo.InvariantCulture);
        Error = null;
        WasActive = false;
    }
}
