namespace Content.Server.Backmen.Drone;

[RegisterComponent]
public sealed partial class BSSDroneComponent : Component
{
    [DataField("droneType")] public string DroneType { get; private set; } = "default";

    public EntityUid? ActionBPLAMEDActionBrutepack = null;
    public EntityUid? ActionBPLAMEDActionOintment = null;
    public EntityUid? ActionBPLAMEDActionBloodpack = null;
}
