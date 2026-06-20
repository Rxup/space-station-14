using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;

namespace Content.Shared.Backmen.Surgery.Pain.Components;

/// <summary>
/// Organ/body-part nerve data used by the pain & wounding systems.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true, raiseAfterAutoHandleState: true)]
public sealed partial class NerveOrganComponent : Component
{
    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadOnly)]
    public FixedPoint2 PainMultiplier = 1.0f;

    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public FixedPoint2 DefaultPainFeels = 1;

    /// <summary>
    /// How feelable pain is on this nerve. Synced from server; modifiers dict is server-only.
    /// </summary>
    [AutoNetworkedField, ViewVariables(VVAccess.ReadOnly)]
    public FixedPoint2 PainFeels = 1.0f;

    [ViewVariables(VVAccess.ReadOnly)]
    public Dictionary<(EntityUid, string), PainFeelingModifier> PainFeelingModifiers = new();

    /// <summary>
    /// Nerve system, to which this nerve is parented.
    /// </summary>
    [AutoNetworkedField, ViewVariables(VVAccess.ReadOnly)]
    public EntityUid ParentedNerveSystem;
}

