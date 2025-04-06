using Content.Shared.Alert;
using Robust.Shared.Prototypes;

namespace Content.Server.Backmen.Drone;

[RegisterComponent]
public sealed partial class BSSDroneComponent : Component
{
    [DataField("droneType")] public string DroneType { get; private set; } = "default";

    /// <summary>
    /// Locale string to popup when there is no power
    /// </summary>
    [DataField(required: true)]
    public LocId NoPowerPopup = string.Empty;

    /// <summary>
    /// Alert to show for suit power.
    /// </summary>
    [DataField]
    public ProtoId<AlertPrototype> DronePowerAlert = "SuitPower";

    public float UpdateTimer = 0;
}
