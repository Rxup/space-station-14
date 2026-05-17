using Robust.Client.GameObjects;
using Robust.Shared.GameStates;

namespace Content.Client._Impstation.Revenant;

[RegisterComponent, NetworkedComponent]
public sealed partial class RevenantAnimatedComponent : Component
{
    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public float Accumulator;

    [ViewVariables(VVAccess.ReadOnly)]
    public EntityUid? LightOverlay;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public Color LightColor = Color.MediumPurple;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float LightRadius = 2f;
}
