namespace Content.Server.Backmen.Drone
{
    [RegisterComponent]
    public sealed class BSSDroneComponent : Component
    {
        [DataField("droneType")] public string DroneType { get; } = "default";
    }
}
