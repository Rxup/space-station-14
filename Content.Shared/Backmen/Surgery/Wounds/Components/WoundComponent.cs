using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared.Backmen.Surgery.Wounds.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true, raiseAfterAutoHandleState: true)]
public sealed partial class WoundComponent : Component
{
    /// <summary>
    /// The 'Parent' of the wound. Basically the entity to which the wound was applied.
    /// </summary>
    [AutoNetworkedField, ViewVariables(VVAccess.ReadOnly)]
    public EntityUid HoldingWoundable;

    /// <summary>
    /// The damage this wound applies to it's woundable
    /// </summary>
    public FixedPoint2 WoundIntegrityDamage => WoundSeverityPoint * WoundableIntegrityMultiplier;

    /// <summary>
    /// The severity of the wound. The more the worse.
    /// Directly depends on <see cref="WoundSeverity"/>
    /// </summary>
    [AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public FixedPoint2 WoundSeverityPoint;

    /// <summary>
    /// How much damage this wound does to it's parent woundable?
    /// </summary>
    [AutoNetworkedField, ViewVariables(VVAccess.ReadOnly), DataField("integrityMultiplier")]
    public FixedPoint2 WoundableIntegrityMultiplier = 1;

    /// <summary>
    /// With what chance will this wound merge when a new one occurs?
    /// </summary>
    [AutoNetworkedField, ViewVariables(VVAccess.ReadOnly), DataField("mergeChance")]
    public FixedPoint2 MergeChance = 0.1;

    /// <summary>
    /// maybe some cool mechanical stuff to treat those wounds later. I genuinely have no idea
    /// Wound type. External/Internal basically.
    /// </summary>
    [AutoNetworkedField, ViewVariables(VVAccess.ReadOnly), DataField]
    public WoundType WoundType = WoundType.External;

    /// <summary>
    /// Self-explanatory
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public TimeSpan DamagedLastTime;

    /// <summary>
    /// Self-explanatory, right now only applies for passive healing; Basically: Clotting
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField]
    public TimeSpan CanHealAfter = TimeSpan.FromSeconds(15f);

    /// <summary>
    /// Damage group of this wound.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly), DataField]
    public DamageGroupPrototype? DamageGroup;

    [AutoNetworkedField]
    public ProtoId<DamageGroupPrototype>? NetworkedDamageGroup;

    /// <summary>
    /// Damage group of this wound.
    /// </summary>
    [AutoNetworkedField, ViewVariables(VVAccess.ReadOnly), DataField(required: true, customTypeSerializer: typeof(PrototypeIdSerializer<DamageTypePrototype>))]
    public string DamageType = default!;

    /// <summary>
    /// Scar wound prototype, what will be spawned upon healing this wound.
    /// If null - no scar wound will be spawned.
    /// </summary>
    [AutoNetworkedField, ViewVariables(VVAccess.ReadOnly), DataField]
    public EntProtoId? ScarWound;

    /// <summary>
    /// Well, name speaks for this.
    /// </summary>
    [AutoNetworkedField, ViewVariables(VVAccess.ReadOnly), DataField]
    public bool IsScar;

    /// <summary>
    /// Wound severity. Has six severities: Healed/Minor/Moderate/Severe/Critical and Loss.
    /// Directly depends on <see cref="WoundSeverityPoint"/>
    /// </summary>
    [AutoNetworkedField, ViewVariables(VVAccess.ReadWrite), DataField]
    public WoundSeverity WoundSeverity;

    /// <summary>
    /// When wound is visible. Always/HandScanner/AdvancedScanner.
    /// </summary>
    [AutoNetworkedField, ViewVariables(VVAccess.ReadWrite), DataField]
    public WoundVisibility WoundVisibility = WoundVisibility.Always;

    /// <summary>
    /// "Can be healed".
    /// </summary>
    [AutoNetworkedField, DataField, ViewVariables(VVAccess.ReadWrite)]
    public bool CanBeHealed = true;
}
