using Content.Shared.Actions;
using Robust.Shared.GameStates;

namespace Content.Shared.Item.Optic;

[RegisterComponent, NetworkedComponent]
public sealed partial class OpticZoomEffectComponent : Component
{
    [DataField("targetZoom"), ViewVariables(VVAccess.ReadOnly)]
    public float TargetZoom { get; set; } = 2;
}

public sealed partial class OpticZoomEffectActionEvent : InstantActionEvent
{

}
