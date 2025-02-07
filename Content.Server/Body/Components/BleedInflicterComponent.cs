using Content.Server.Body.Systems;
using Content.Shared.Backmen.Surgery.Wounds.Systems;
using Content.Shared.FixedPoint;

namespace Content.Server.Body.Components;

[RegisterComponent, Access(typeof(BloodstreamSystem), typeof(WoundSystem))]
public sealed partial class BleedInflicterComponent : Component
{
    [ViewVariables(VVAccess.ReadOnly)]
    public bool IsBleeding = false;

    [ViewVariables(VVAccess.ReadOnly)]
    public FixedPoint2 BleedingAmount => BleedingAmountRaw * Scaling;

    [ViewVariables(VVAccess.ReadOnly)]
    public FixedPoint2 BleedingAmountRaw = 0;

    [DataField("canGrow"), ViewVariables(VVAccess.ReadOnly)]
    public bool BleedingScales = true;

    // it's calculated when a wound is spawned.
    [ViewVariables(VVAccess.ReadOnly)]
    public TimeSpan ScalingFinishesAt = TimeSpan.Zero;

    [ViewVariables(VVAccess.ReadOnly)]
    public TimeSpan ScalingStartsAt = TimeSpan.Zero;

    [ViewVariables(VVAccess.ReadOnly)]
    public FixedPoint2 SeverityPenalty = 1f;

    [ViewVariables(VVAccess.ReadOnly)]
    public FixedPoint2 Scaling = 1;

    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public FixedPoint2 ScalingLimit = 2.4;
}
