using Content.Shared._Backmen.Disease;

namespace Content.Server._Backmen.Disease.Server;

[RegisterComponent]
public sealed partial class DiseaseServerComponent : Component
{
    /// <summary>
    /// Which diseases this server has information on.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public List<DiseasePrototype> Diseases = new();
}
