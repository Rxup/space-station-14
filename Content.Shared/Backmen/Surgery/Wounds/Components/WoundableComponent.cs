using Content.Shared.Backmen.Surgery.Traumas;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Backmen.Surgery.Wounds.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true, raiseAfterAutoHandleState: true)]
public sealed partial class WoundableComponent : Component
{
    /// <summary>
    /// UID of the parent woundable entity. Can be null.
    /// </summary>
    [AutoNetworkedField, ViewVariables]
    public EntityUid? ParentWoundable;

    /// <summary>
    /// UID of the root woundable entity.
    /// </summary>
    [AutoNetworkedField, ViewVariables]
    public EntityUid RootWoundable;

    /// <summary>
    /// Set of UIDs representing child woundable entities.
    /// </summary>
    [AutoNetworkedField, ViewVariables]
    public HashSet<EntityUid> ChildWoundables = [];

    /// <summary>
    /// Indicates whether wounds are allowed.
    /// </summary>
    [AutoNetworkedField, DataField, ViewVariables]
    public bool AllowWounds = true;

    /// <summary>
    /// The same as DamageableComponent's one
    /// </summary>
    [AutoNetworkedField, DataField("damageContainer"), ViewVariables]
    public ProtoId<DamageContainerPrototype>? DamageContainer;

    [DataField]
    public EntProtoId BoneEntity = "Bone";

    /// <summary>
    /// Integrity points of this woundable.
    /// </summary>
    [ViewVariables, DataField]
    public FixedPoint2 IntegrityCap;

    /// <summary>
    /// How big is the Woundable Entity, mostly used for trauma calculation, dodging and targeting
    /// </summary>
    [AutoNetworkedField, ViewVariables, DataField]
    public FixedPoint2 DodgeChance = 0.1;

    /// <summary>
    /// Integrity points of this woundable.
    /// </summary>
    [AutoNetworkedField, ViewVariables, DataField("integrity")]
    public FixedPoint2 WoundableIntegrity;

    /// <summary>
    /// yeah
    /// </summary>
    [ViewVariables, DataField(required: true)]
    public Dictionary<WoundableSeverity, FixedPoint2> Thresholds = new();

    [ViewVariables, DataField]
    public Dictionary<TraumaType, FixedPoint2> PassiveTraumaChances = new()
    {
        { TraumaType.BoneDamage, 0 },
        { TraumaType.OrganDamage, 0 },
        { TraumaType.Dismemberment, 0 },
        { TraumaType.NerveDamage, 0 },
        { TraumaType.VeinsDamage, 0 },
    };

    /// <summary>
    /// How much damage will be healed ACROSS all limb, for example if there are 2 wounds,
    /// Healing will be shared across those 2 wounds.
    /// </summary>
    [AutoNetworkedField, ViewVariables, DataField]
    public FixedPoint2 HealAbility = 0.1f;

    /// <summary>
    /// How much bleeds will the woundable treat per tick
    /// </summary>
    [ViewVariables, DataField]
    public FixedPoint2 BleedingTreatmentAbility = 0.04f;

    /// <summary>
    /// At which amount of bleeds the woundable will stop healing.
    /// </summary>
    [ViewVariables, DataField]
    public FixedPoint2 BleedsThreshold = 2.4f;

    /// <summary>
    /// Multipliers of severity applied to this wound.
    /// </summary>
    public Dictionary<EntityUid, WoundableSeverityMultiplier> SeverityMultipliers = new();

    /// <summary>
    /// Multipliers applied to healing rate.
    /// </summary>
    public Dictionary<EntityUid, WoundableHealingMultiplier> HealingMultipliers = new();

    /// <summary>
    /// Fraction of lost integrity that must come from burn wounds to vaporize into ash.
    /// </summary>
    [DataField]
    public float? BurnDominanceRatio;

    [DataField]
    public SoundSpecifier WoundableDestroyedSound = new SoundCollectionSpecifier("WoundableDestroyed");

    [DataField]
    public SoundSpecifier WoundableDelimbedSound = new SoundCollectionSpecifier("WoundableDelimbed");

    /// <summary>
    /// State of the woundable. Severity basically.
    /// </summary>
    [AutoNetworkedField, ViewVariables, DataField]
    public WoundableSeverity WoundableSeverity;

    /// <summary>
    /// How much time in seconds had this woundable accumulated from the last healing tick?
    /// </summary>
    [AutoNetworkedField, ViewVariables]
    public float HealingRateAccumulated;

    /// <summary>
    /// Container potentially holding wounds.
    /// </summary>
    [ViewVariables]
    public Container Wounds;

    [ViewVariables]
    public Dictionary<ProtoId<DamageTypePrototype>, List<Entity<WoundComponent>>> WoundsByDamageType = new();

    /// <summary>
    /// Container holding this woundable's bone.
    /// </summary>
    [ViewVariables]
    public Container Bone;
}
