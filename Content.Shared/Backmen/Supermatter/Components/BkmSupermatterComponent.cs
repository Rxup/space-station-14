using Content.Shared.Atmos;
using Content.Shared.Tag;
using Content.Shared.Whitelist;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Backmen.Supermatter.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BkmSupermatterComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite)]
    public float Power;

    /// <summary>
    /// The amount of damage we have currently
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public float Damage = 0f;

    [ViewVariables(VVAccess.ReadWrite)]
    public float MatterPower;

    [ViewVariables(VVAccess.ReadWrite)]
    public float MatterPowerConversion = 10f;

    /// <summary>
    /// The portion of the gasmix we're on
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public float GasEfficiency = 0.15f;

    /// <summary>
    /// The amount of heat we apply scaled
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public float HeatThreshold = 2500f;

    /// <summary>
    /// Is used to store gas
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    [DataField("gasStorage")]
    public Dictionary<Gas, float> GasStorage = new Dictionary<Gas, float>()
    {
        {Gas.Oxygen, 0f},
        {Gas.Nitrogen, 0f},
        {Gas.NitrousOxide, 0f},
        {Gas.CarbonDioxide, 0f},
        {Gas.Plasma, 0f},
        {Gas.Tritium, 0f},
        {Gas.WaterVapor, 0f},
        {Gas.Frezon, 0f},
        {Gas.Ammonia, 0f}
    };

    /// <summary>
    /// Stores each gases calculation
    /// </summary>
    public readonly Dictionary<Gas, (float TransmitModifier, float HeatPenalty, float PowerMixRatio)> GasDataFields = new()
    {
        [Gas.Oxygen] = (TransmitModifier: 1.5f, HeatPenalty: 1f, PowerMixRatio: 1f),
        [Gas.Nitrogen] = (TransmitModifier: 0f, HeatPenalty: -1.5f, PowerMixRatio: -1f),
        [Gas.NitrousOxide] = (TransmitModifier: 1f, HeatPenalty: -5f, PowerMixRatio: 1f),
        [Gas.CarbonDioxide] = (TransmitModifier: 0f, HeatPenalty: 0.1f, PowerMixRatio: 1f),
        [Gas.Plasma] = (TransmitModifier: 4f, HeatPenalty: 15f, PowerMixRatio: 1f),
        [Gas.Tritium] = (TransmitModifier: 30f, HeatPenalty: 10f, PowerMixRatio: 1f),
        [Gas.WaterVapor] = (TransmitModifier: 2f, HeatPenalty: 12f, PowerMixRatio: 1f),
        [Gas.Frezon] = (TransmitModifier: 3f, HeatPenalty: -9f, PowerMixRatio: -1f),
        [Gas.Ammonia] = (TransmitModifier: 1.5f, HeatPenalty: 1.5f, PowerMixRatio: 1.5f)
    };

    public EntProtoId[] LightningPrototypes =
    {
        "Lightning",
        "ChargedLightning",
        "SuperchargedLightning",
        "HyperchargedLightning"
    };

    [DataField]
    public EntProtoId SingularitySpawnPrototype = "Singularity";

    [DataField]
    public EntProtoId TeslaSpawnPrototype = "TeslaEnergyBall";

    //[DataField]
    //public EntProtoId KudzuSpawnPrototype = "SupermatterKudzu";
}
