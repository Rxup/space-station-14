using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;

namespace Content.Shared.Backmen.Surgery.Pain.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class PainInflicterComponent : Component
{
    [DataField, ViewVariables(VVAccess.ReadOnly), AutoNetworkedField]
    public FixedPoint2 Pain;

    // Some wounds hurt harder.
    [DataField("multiplier"), ViewVariables(VVAccess.ReadOnly), AutoNetworkedField]
    public FixedPoint2 PainMultiplier = 1;
}
