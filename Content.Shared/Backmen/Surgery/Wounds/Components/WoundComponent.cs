using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared.Backmen.Surgery.Wounds.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class WoundComponent : Component
{
    /// <summary>
    /// 'Parent' of wound. Basically the entity to which the wound was applied.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public EntityUid HoldingWoundable;

    /// <summary>
    /// The damage this wound applies to it's woundable
    /// </summary>
    public FixedPoint2 WoundIntegrityDamage => WoundSeverityPoint * WoundableIntegrityMultiplier;

    /// <summary>
    /// Actually, severity of the wound. The more the worse.
    /// Directly depends on <see cref="WoundSeverity"/>
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public FixedPoint2 WoundSeverityPoint;

    /// <summary>
    /// How much damage this wound does to it's parent woundable?
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly), DataField("integrityMultiplier")]
    public FixedPoint2 WoundableIntegrityMultiplier = 1;

    /// <summary>
    /// maybe some cool mechanical stuff to treat those wounds later. I genuinely have no idea
    /// Wound type. External/Internal basically.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly), DataField]
    public WoundType WoundType = WoundType.External;

    /// <summary>
    /// Damage group of this wound.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly), DataField]
    public DamageGroupPrototype? DamageGroup;

    /// <summary>
    /// Damage group of this wound.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly), DataField(required: true, customTypeSerializer: typeof(PrototypeIdSerializer<DamageTypePrototype>))]
    public string DamageType;

    /// <summary>
    /// Scar wound prototype, what will be spawned upon healing this wound.
    /// If null - no scar wound will be spawned.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly), DataField]
    public EntProtoId? ScarWound;

    /// <summary>
    /// Well, name speaks for this.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly), DataField]
    public bool IsScar;

    /// <summary>
    /// Wound severity. Has six severities: Healed/Minor/Moderate/Severe/Critical and Loss.
    /// Directly depends on <see cref="WoundSeverityPoint"/>
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField]
    public WoundSeverity WoundSeverity;

    /// <summary>
    /// When wound is visible. Always/HandScanner/AdvancedScanner.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField]
    public WoundVisibility WoundVisibility = WoundVisibility.Always;

    /// <summary>
    /// "Can be healed".
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public bool CanBeHealed = true;
}

[Serializable, NetSerializable]
public sealed class WoundComponentState : ComponentState
{
    public NetEntity HoldingWoundable;

    public FixedPoint2 WoundSeverityPoint;
    public FixedPoint2 WoundableIntegrityMultiplier;

    public WoundType WoundType;

    public DamageGroupPrototype? DamageGroup;
    public string? DamageType;

    public EntProtoId? ScarWound;

    public bool IsScar;

    public WoundSeverity WoundSeverity;

    public WoundVisibility WoundVisibility;

    public bool CanBeHealed;
}
