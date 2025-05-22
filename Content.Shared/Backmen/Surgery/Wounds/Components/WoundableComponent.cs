﻿using Content.Shared.Backmen.Surgery.Traumas;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Backmen.Surgery.Wounds.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class WoundableComponent : Component
{
    /// <summary>
    /// UID of the parent woundable entity. Can be null.
    /// </summary>
    [ViewVariables]
    public EntityUid? ParentWoundable;

    /// <summary>
    /// UID of the root woundable entity.
    /// </summary>
    [ViewVariables]
    public EntityUid RootWoundable;

    /// <summary>
    /// Set of UIDs representing child woundable entities.
    /// </summary>
    [ViewVariables]
    public HashSet<EntityUid> ChildWoundables = [];

    /// <summary>
    /// Indicates whether wounds are allowed.
    /// </summary>
    [DataField]
    [ViewVariables]
    public bool AllowWounds = true;

    /// <summary>
    /// The same as DamageableComponent's one
    /// </summary>
    [DataField("damageContainer")]
    public ProtoId<DamageContainerPrototype>? DamageContainerID;

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
    [ViewVariables, DataField]
    public FixedPoint2 DodgeChance = 0.1;

    /// <summary>
    /// Integrity points of this woundable.
    /// </summary>
    [ViewVariables, DataField("integrity")]
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
    [ViewVariables, DataField]
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

    [DataField]
    public SoundSpecifier WoundableDestroyedSound = new SoundCollectionSpecifier("WoundableDestroyed");

    [DataField]
    public SoundSpecifier WoundableDelimbedSound = new SoundCollectionSpecifier("WoundableDelimbed");

    /// <summary>
    /// State of the woundable. Severity basically.
    /// </summary>
    [ViewVariables, DataField]
    public WoundableSeverity WoundableSeverity;

    /// <summary>
    /// How much time in seconds had this woundable accumulated from the last healing tick.
    /// </summary>
    [ViewVariables]
    public float HealingRateAccumulated;

    /// <summary>
    /// Container potentially holding wounds.
    /// </summary>
    [ViewVariables]
    public Container? Wounds;

    /// <summary>
    /// Container holding this woundables bone.
    /// </summary>
    [ViewVariables]
    public Container Bone;
}

[Serializable, NetSerializable]
public sealed class WoundableComponentState : ComponentState
{
    public NetEntity? ParentWoundable;
    public NetEntity RootWoundable;

    public HashSet<NetEntity> ChildWoundables = [];

    public bool AllowWounds = true;

    public ProtoId<DamageContainerPrototype>? DamageContainerID;

    public FixedPoint2 DodgeChance;

    public FixedPoint2 WoundableIntegrity;
    public FixedPoint2 HealAbility;

    public Dictionary<NetEntity, WoundableSeverityMultiplier> SeverityMultipliers = new();
    public Dictionary<NetEntity, WoundableHealingMultiplier> HealingMultipliers = new();

    public WoundableSeverity WoundableSeverity;

    public float HealingRateAccumulated;
}
