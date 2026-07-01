namespace Content.Shared.Backmen.Tools.Components;

/// <summary>
/// Blocks tool interactions on structures (e.g. ship weapons that must stay anchored).
/// Handled by <see cref="DisableToolUseSystem"/>.
/// </summary>
[RegisterComponent]
public sealed partial class DisableToolUseComponent : Component
{
    [DataField]
    public bool Anchoring;

    [DataField]
    public bool Prying;

    [DataField]
    public bool Screwing;

    [DataField]
    public bool Cutting;

    [DataField]
    public bool Welding;

    [DataField]
    public bool Pulsing;

    [DataField]
    public bool Slicing;

    [DataField]
    public bool Sawing;

    [DataField]
    public bool Honking;

    [DataField]
    public bool Rolling;

    [DataField]
    public bool Digging;
}
