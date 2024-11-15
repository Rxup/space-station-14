using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;

namespace Content.Shared.ADT.Mech.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MechOverloadComponent : Component
{
    [AutoNetworkedField]
    public EntityUid? MechOverloadActionEntity;

    [DataField]
    public EntProtoId MechOverloadAction = "ActionMechOverload";

    [ViewVariables(VVAccess.ReadWrite)]
    public bool Overload = false;

    /// <summary>
    /// damage every x seconds if mech overloaded.
    /// </summary>

    [DataField("damage", required: true)]
    [ViewVariables(VVAccess.ReadWrite)]
    public DamageSpecifier DamagePerSpeed = default!;
    /// <summary>
    /// How much "health" the mech has left.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public FixedPoint2 MinIng;

    public float Accumulator = 0f;
}
