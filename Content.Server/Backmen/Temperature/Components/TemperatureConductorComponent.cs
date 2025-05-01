// ReSharper disable once CheckNamespace
namespace Content.Server.Temperature.Components;

/// <summary>
/// Adding this component will add temperature conduction support to the entity.
/// Mainly copied from InternalTemperatureComponent, but it's not always required, so this is a separate thing.
/// </summary>
[RegisterComponent]
public sealed partial class TemperatureConductorComponent : Component
{
    /// <summary>
    /// Thermal conductivity of the material in W/m/K.
    /// Higher conductivity means its insides will heat up faster.
    /// </summary>
    [DataField]
    public float Conductivity = 0.5f;

    /// <summary>
    /// Average thickness between the surface and the inside.
    /// For meats and such this is constant.
    /// Thicker materials take longer for heat to dissipate.
    /// </summary>
    [DataField(required: true)]
    public float Thickness;

    /// <summary>
    /// Surface area in m^2 for the purpose of conducting surface temperature to the inside.
    /// Larger surface area means it takes longer to heat up/cool down
    /// </summary>
    /// <remarks>
    /// For meats etc this should just be the area of the cooked surface not the whole thing as it's only getting heat from one side usually.
    /// </remarks>
    [DataField(required: true)]
    public float Area;
}
