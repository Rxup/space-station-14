// ReSharper disable once CheckNamespace
namespace Content.Server.Temperature.Components;

/// <summary>
/// This is used for...
/// </summary>
[RegisterComponent]
public sealed partial class AddTemperatureOnTriggerComponent : Component
{
    [DataField]
    public bool IgnoreResistance = true;

    [DataField]
    public float HeatAmount = 10000f;
}
