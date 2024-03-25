using Content.Shared.Backmen.Disease;

namespace Content.Server.Backmen.Disease.Components;

/// <summary>
/// For disease vaccines
/// </summary>

[RegisterComponent]
public sealed partial class DiseaseVaccineComponent : Component
{
    /// <summary>
    /// How long it takes to inject someone
    /// </summary>
    [DataField("injectDelay")]
    public float InjectDelay = 2f;
    /// <summary>
    /// If this vaccine has been used
    /// </summary>
    public bool Used = false;

    /// <summary>
    /// The disease prototype currently on the vaccine
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public DiseasePrototype? Disease;
}
