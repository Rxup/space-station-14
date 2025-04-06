namespace Content.Shared.Backmen.BluespaceMining;

/// <summary>
/// Holds the off and running state for machines to control
/// playing animations on the client.
/// </summary>
[RegisterComponent]
public sealed partial class BluespaceMinerVisualsComponent : Component
{
    [DataField(required: true)]
    public string OffState = default!;

    [DataField(required: true)]
    public string RunningState = default!;
}
