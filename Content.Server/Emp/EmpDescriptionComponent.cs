namespace Content.Server.Emp;

/// <summary>
/// Generates an EMP description for an entity that won't otherwise get one.
/// </summary>
[RegisterComponent]
[Access(typeof(EmpSystem))]
public sealed partial class EmpDescriptionComponent : Component
{
    [DataField]
    public float Range = 1.0f;

    [DataField]
    public float EnergyConsumption;

    [DataField]
    public float DisableDuration = 10f;
}
