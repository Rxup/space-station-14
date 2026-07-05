using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.Backmen.Body.Components;

/// <summary>
/// Balance and immunity wiring for cosmic carp organs (space lungs / space heart).
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SpaceAnimalOrganComponent : Component
{
    [DataField]
    public float DropChance = 0.55f;

    [DataField]
    public float HarvestDamageFraction = 0.35f;

    [DataField]
    public FixedPoint2 HumanIntegrityCap = 8;

    [DataField]
    public EntProtoId? HeartStatusEffect;

    [DataField]
    public EntProtoId? LungsStatusEffect;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan OrganRotAfter = TimeSpan.FromMinutes(3);
}
