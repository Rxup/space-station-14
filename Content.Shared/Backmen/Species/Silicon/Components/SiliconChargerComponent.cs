using Content.Shared.Storage.Components;
using Content.Shared.StepTrigger.Components;
using Robust.Shared.Audio;

namespace Content.Shared.Backmen.Silicon;

[RegisterComponent]
public sealed partial class SiliconChargerComponent : Component
{
    /// <summary>
    ///     Is the charger currently active?
    /// </summary>
    public bool Active = false;

    /// <summary>
    ///     The currently playing audio stream.
    /// </summary>
    public EntityUid? SoundStream { get; set; }

    /// <summary>
    ///     Counter for handing out warnings to burning entities.
    /// </summary>
    public TimeSpan WarningTime = TimeSpan.Zero;

    /// <summary>
    ///     The current parts multiplier.
    /// </summary>
    public float PartsChargeMulti = 1.2f;


    /// <summary>
    ///     The sound to play when the charger is active.
    /// </summary>
    [DataField("soundLoop")]
    public SoundSpecifier SoundLoop = new SoundPathSpecifier("/Audio/Machines/microwave_loop.ogg");

    /// <summary>
    ///     The multiplier for the charge rate.
    ///     For reference, an IPC drains at 50.
    /// </summary>
    [DataField("chargeMulti"), ViewVariables(VVAccess.ReadWrite)]
    public float ChargeMulti = 50f;

    /// <summary>
    ///     The minimum size of a battery to be charged.
    /// </summary>
    /// <remarks>
    ///     Charging a battery too small will detonate it, becoming more likely as it fills.
    /// </remarks>
    [DataField("minChargeSize"), ViewVariables(VVAccess.ReadWrite)]
    public int MinChargeSize = 1000;

    /// <summary>
    ///     The minimum amount of time it will take to charge a battery, in seconds.
    /// </summary>
    /// <remarks>
    ///     Note that this is from empty. A battery that is already half full will take half as long as this value to reach full, if it would've been faster from empty.
    ///     This is for the sake of feeling cooler- It's lame to just charge instantly.
    /// </remarks>
    [DataField("minChargeTime"), ViewVariables(VVAccess.ReadWrite)]
    public float MinChargeTime = 10f;

    /// <summary>
    ///     The temperature the charger will stop heating up at.
    /// </summary>
    /// <remarks>
    ///     Used specifically for chargers with the <see cref="SharedEntityStorageComponent"/>.
    /// </remarks>
    [DataField("targetTemp"), ViewVariables(VVAccess.ReadWrite)]
    public float TargetTemp = 373.15f;

    /// <summary>
    ///     The damage type to deal when a Biological entity is burned.
    /// </summary>
    [DataField("damageType")]
    public string DamageType = "Shock";

    /// <summary>
    ///     The modifier to apply to a used parts rating.
    /// </summary>
    /// <remarks>
    ///     0.6 is the default as it provides a nice range where 2 is about normal, and 4 is about two and a half.
    /// </remarks>
    [DataField("upgradePartsMulti"), ViewVariables(VVAccess.ReadWrite)]
    public float UpgradePartsMulti = 0.6f;

    /// <summary>
    ///     The part to be used for the charge speed.
    /// </summary>
    [DataField("chargeSpeedPart")]
    public string ChargeSpeedPart = "Capacitor";

    /// <summary>
    ///     The part to be used for the charge efficiency.
    /// </summary>
    [DataField("chargeEfficiencyPart")]
    public string ChargeEfficiencyPart = "Manipulator";


    /// <summary>
    ///     Charger overheat string
    /// </summary>
    [DataField("overheatString")]
    public string OverheatString = "silicon-charger-overheatwarning";


    /// <summary>
    ///     The list of entities currently stood on a charger.
    /// </summary>
    /// <remarks>
    ///     Used specifically for chargers with the <see cref="StepTriggerComponent"/>.
    /// </remarks>
    [ViewVariables(VVAccess.ReadOnly)]
    public List<EntityUid> PresentEntities = new List<EntityUid>();

    /// <summary>
    ///     The number of entities that can be stood on a charger at once.
    /// <summary>
    /// <remarks>
    ///     Used specifically for chargers with the <see cref="StepTriggerComponent"/>.
    /// </remarks>
    [DataField("maxEntities"), ViewVariables(VVAccess.ReadWrite)]
    public int MaxEntities = 1;
}
