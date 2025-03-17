using Content.Shared.FixedPoint;
using Content.Shared.Whitelist;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Audio;
using Content.Shared.Damage;

namespace Content.Shared.Mech.Components;

/// <summary>
/// A large, pilotable machine that has equipment that is
/// powered via an internal battery.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MechComponent : Component
{
    /// <summary>
    /// How much "health" the mech has left.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public FixedPoint2 Integrity;

    /// <summary>
    /// The maximum amount of damage the mech can take.
    /// </summary>
    [DataField("maxintegrity"), AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]  // ADT Mech
    public FixedPoint2 MaxIntegrity = 250;

    /// <summary>
    /// How much energy the mech has.
    /// Derived from the currently inserted battery.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public FixedPoint2 Energy = 0;

    /// <summary>
    /// The maximum amount of energy the mech can have.
    /// Derived from the currently inserted battery.
    /// </summary>
    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public FixedPoint2 MaxEnergy = 0;

    /// <summary>
    /// The slot the battery is stored in.
    /// </summary>
    [ViewVariables]
    public ContainerSlot BatterySlot = default!;

    [ViewVariables]
    public readonly string BatterySlotId = "mech-battery-slot";

    /// <summary>
    /// A multiplier used to calculate how much of the damage done to a mech
    /// is transfered to the pilot
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float MechToPilotDamageMultiplier;

    /// <summary>
    /// Whether the mech has been destroyed and is no longer pilotable.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public bool Broken = false;

    /// <summary>
    /// The slot the pilot is stored in.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public ContainerSlot PilotSlot = default!;

    [ViewVariables]
    public readonly string PilotSlotId = "mech-pilot-slot";

    /// <summary>
    /// The current selected equipment of the mech.
    /// If null, the mech is using just its fists.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public EntityUid? CurrentSelectedEquipment;

    /// <summary>
    /// The maximum amount of equipment items that can be installed in the mech
    /// </summary>
    [DataField("maxEquipmentAmount"), ViewVariables(VVAccess.ReadWrite)]
    public int MaxEquipmentAmount = 3;

    /// <summary>
    /// A whitelist for inserting equipment items.
    /// </summary>
    [DataField("equipmentWhitelist")]   // ADT Mech
    public EntityWhitelist? EquipmentWhitelist;

    [DataField]
    public EntityWhitelist? PilotWhitelist;

    /// <summary>
    /// A container for storing the equipment entities.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public Container EquipmentContainer = default!;

    [ViewVariables]
    public readonly string EquipmentContainerId = "mech-equipment-container";

    /// <summary>
    /// How long it takes to enter the mech.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float EntryDelay = 3;

    /// <summary>
    /// How long it takes to pull *another person*
    /// outside of the mech. You can exit instantly yourself.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float ExitDelay = 3;

    /// <summary>
    /// How long it takes to pull out the battery.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float BatteryRemovalDelay = 2;

    /// <summary>
    /// Whether or not the mech is airtight.
    /// </summary>
    /// <remarks>
    /// This needs to be redone
    /// when mech internals are added
    /// </remarks>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public bool Airtight;

    /// <summary>
    /// The equipment that the mech initially has when it spawns.
    /// Good for things like nukie mechs that start with guns.
    /// </summary>
    [DataField]
    public List<EntProtoId> StartingEquipment = new();

    // ADT Content Start
    /// <summary>
    /// sound if access denied
    /// </summary>
    [DataField("accessDeniedSound")]
    public SoundSpecifier AccessDeniedSound = new SoundPathSpecifier("/Audio/Machines/Nuke/angry_beep.ogg");
    /// <summary>
    /// sound on equipment destroyed
    /// </summary>
    [DataField]
    public SoundSpecifier EquipmentDestroyedSound = new SoundCollectionSpecifier("MetalBreak");
    /// <summary>
    /// sound on entry mech
    /// </summary>
    [DataField("mechentrysound")]
    public SoundSpecifier MechEntrySound = new SoundPathSpecifier("/Audio/ADT/Mecha/nominal.ogg");
    /// <summary>
    /// sound on turn lights
    /// </summary>
    [DataField("mechlightoffsound")]
    public SoundSpecifier MechLightsOnSound = new SoundPathSpecifier("/Audio/ADT/Mecha/mechlignton.ogg");
    [DataField("mechlightonsound")]
    public SoundSpecifier MechLightsOffSound = new SoundPathSpecifier("/Audio/ADT/Mecha/mechlightoff.ogg");
    /// <summary>
    /// sound on destroy equipment
    /// </summary>
    [DataField("mechequipmentdestr")]
    public SoundSpecifier MechEquipmentDestr = new SoundPathSpecifier("/Audio/ADT/Mecha/weapdestr.ogg");

    /// <summary>
    /// How much "health" the mech need to destroy equipment.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public FixedPoint2 DamageToDesEqi = 75;
    /// <summary>
    /// How much "health" the mech has left.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public float EMPDamage = 700;

    /// <summary>
    /// damage modifiers on hit
    /// </summary>
    [DataField]
    public DamageModifierSet? Modifiers;

    /// <summary>
    /// A multiplier used to calculate how much of the damage done to a mech
    /// is transfered to the pilot
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float MechEnergyWaste = 10;

    /// <summary>
    /// A blacklist for inserting equipment items.
    /// </summary>
    [DataField]
    public EntityWhitelist? BatteryBlacklist;

    /// <summary>
    /// Is lights currently toggled
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public bool LightsToggle = false;
    // ADT Content End

    #region Action Prototypes
    [DataField]
    public EntProtoId MechCycleAction = "ActionMechCycleEquipment";
    [DataField]
    public EntProtoId MechUiAction = "ActionMechOpenUI";
    [DataField]
    public EntProtoId MechEjectAction = "ActionMechEject";

    // ADT Content Start
    public EntProtoId MechTurnLightsAction = "ActionMechTurnLights";
    public EntProtoId MechInhaleAction = "ActionMechInhale";
    // ADT Content End
    #endregion

    #region Visualizer States
    [DataField]
    public string? BaseState;
    [DataField]
    public string? OpenState;
    [DataField]
    public string? BrokenState;
    #endregion

    [DataField] public EntityUid? MechCycleActionEntity;
    [DataField] public EntityUid? MechUiActionEntity;
    [DataField] public EntityUid? MechEjectActionEntity;
    // ADT Content Start
    [DataField] public EntityUid? MechInhaleActionEntity;
    [DataField] public EntityUid? MechTurnLightsActionEntity;
    // ADT Content End
}
