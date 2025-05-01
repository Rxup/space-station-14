// ReSharper disable once CheckNamespace
namespace Content.Server.Temperature.Components;

[RegisterComponent]
public sealed partial class TemperatureExamineComponent : Component
{
    [DataField]
    public bool ShowInnerTemperature = true;
}
