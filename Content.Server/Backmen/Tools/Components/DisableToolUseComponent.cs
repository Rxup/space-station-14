using Robust.Shared.GameStates;

namespace Content.Server.Backmen.Tools.Components;

[RegisterComponent, NetworkedComponent]
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
