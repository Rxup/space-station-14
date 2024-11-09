﻿using Content.Shared.Backmen.Surgery.Tools;
using Content.Shared.Backmen.Targeting;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Body.Part;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedBodySystem))]
public sealed partial class BodyPartComponent : Component, ISurgeryToolComponent
{
    // Need to set this on container changes as it may be several transform parents up the hierarchy.
    /// <summary>
    /// Parent body for this part.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? Body;

    [DataField, AutoNetworkedField]
    public EntityUid? OriginalBody;

    [DataField, AutoNetworkedField]
    public BodyPartSlot? ParentSlot;

    [DataField, AutoNetworkedField]
    public BodyPartType PartType = BodyPartType.Other;

    // TODO BODY Replace with a simulation of organs
    /// <summary>
    ///     Whether or not the owning <see cref="Body"/> will die if all
    ///     <see cref="BodyComponent"/>s of this type are removed from it.
    /// </summary>
    [DataField("vital"), AutoNetworkedField]
    public bool IsVital;

    /// <summary>
    /// Amount of damage to deal when the part gets removed.
    /// Only works if IsVital is true.
    /// </summary>
    [DataField, AutoNetworkedField]
    public FixedPoint2 VitalDamage = 100;

    [DataField, AutoNetworkedField]
    public BodyPartSymmetry Symmetry = BodyPartSymmetry.None;

    [DataField]
    public string ToolName { get; set; } = "A body part";

    [DataField, AutoNetworkedField]
    public bool? Used { get; set; } = null;

    /// <summary>
    /// Child body parts attached to this body part.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Dictionary<string, BodyPartSlot> Children = new();

    /// <summary>
    /// Organs attached to this body part.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Dictionary<string, OrganSlot> Organs = new();

    /// <summary>
    /// How much health the body part has until it pops out.
    /// </summary>
    [ViewVariables]
    public float TotalDamage => Damage.GetTotal().Float();

    /// <summary>
    /// The DamageSpecifier that contains all types of damage that the BodyPart can take.
    /// TODO: Rework this with DamageableComponent
    /// </summary>
    [DataField, AutoNetworkedField]
    public DamageSpecifier Damage = new()
    {
        DamageDict = new Dictionary<string, FixedPoint2>
        {
            { "Blunt", 0 },
            { "Slash", 0 },
            { "Piercing", 0 },
            { "Heat", 0 },
            { "Cold", 0 },
            { "Shock", 0 },
            { "Caustic", 0 },
        }
    };

    [DataField, AutoNetworkedField]
    public float MinIntegrity = 0;

    /// <summary>
    /// The total damage that has to be dealt to a body part
    /// to make possible severing it.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float SeverIntegrity = 70;

    /// <summary>
    /// On what TargetIntegrity we should re-enable the part.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TargetIntegrity EnableIntegrity = TargetIntegrity.ModeratelyWounded;

    [DataField, AutoNetworkedField]
    public Dictionary<TargetIntegrity, float> IntegrityThresholds = new()
    {
        { TargetIntegrity.CriticallyWounded, 70 },
        { TargetIntegrity.HeavilyWounded, 56 },
        { TargetIntegrity.ModeratelyWounded, 42 },
        { TargetIntegrity.SomewhatWounded, 28},
        { TargetIntegrity.LightlyWounded, 17 },
        { TargetIntegrity.Healthy, 7 },
    };

    /// <summary>
    /// Whether this body part is enabled or not.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Enabled = true;

    /// <summary>
    /// How long it takes to run another self heal tick on the body part.
    /// </summary>
    [DataField("healingTime")]
    public float HealingTime = 30;

    /// <summary>
    /// How long it has been since the last self heal tick on the body part.
    /// </summary>
    public float HealingTimer = 0;

    /// <summary>
    /// How much health to heal on the body part per tick.
    /// </summary>
    [DataField("selfHealingAmount")]
    public float SelfHealingAmount = 5;

    [DataField]
    public string ContainerName { get; set; } = "part_slot";

    [DataField, AutoNetworkedField]
    public ItemSlot ItemInsertionSlot = new();

    /// <summary>
    /// These are only for VV/Debug do not use these for gameplay/systems
    /// </summary>
    [ViewVariables]
    private List<ContainerSlot> BodyPartSlotsVV
    {
        get
        {
            List<ContainerSlot> temp = new();
            var containerSystem = IoCManager.Resolve<IEntityManager>().System<SharedContainerSystem>();

            foreach (var slotId in Children.Keys)
            {
                temp.Add((ContainerSlot) containerSystem.GetContainer(Owner, SharedBodySystem.PartSlotContainerIdPrefix+slotId));
            }

            return temp;
        }
    }

    [ViewVariables]
    private List<ContainerSlot> OrganSlotsVV
    {
        get
        {
            List<ContainerSlot> temp = new();
            var containerSystem = IoCManager.Resolve<IEntityManager>().System<SharedContainerSystem>();

            foreach (var slotId in Organs.Keys)
            {
                temp.Add((ContainerSlot) containerSystem.GetContainer(Owner, SharedBodySystem.OrganSlotContainerIdPrefix+slotId));
            }

            return temp;
        }
    }
}

/// <summary>
/// Contains metadata about a body part in relation to its slot.
/// </summary>
[NetSerializable, Serializable]
[DataRecord]
public partial struct BodyPartSlot
{
    public string Id;
    public BodyPartType Type;

    public BodyPartSlot(string id, BodyPartType type)
    {
        Id = id;
        Type = type;
    }
};

/// <summary>
/// Contains metadata about an organ part in relation to its slot.
/// </summary>
[NetSerializable, Serializable]
[DataRecord]
public partial struct OrganSlot
{
    public string Id;

    public OrganSlot(string id)
    {
        Id = id;
    }
};
