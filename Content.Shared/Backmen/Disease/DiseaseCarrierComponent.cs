using System.Linq;
using Content.Shared.Backmen.Disease;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Set;

namespace Content.Shared.Backmen.Disease;

/// <summary>
/// Allows the entity to be infected with diseases.
/// Please use only on mobs.
/// </summary>

[RegisterComponent]
[NetworkedComponent]
public sealed partial class DiseaseCarrierComponent : Component
{
    /// <summary>
    /// Shows the CURRENT diseases on the carrier
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public List<DiseasePrototype> Diseases = new();

    /// <summary>
    /// The carrier's resistance to disease
    /// </summary>
    [DataField("diseaseResist")]
    [ViewVariables(VVAccess.ReadWrite)]
    public float DiseaseResist = 0f;

    /// <summary>
    /// Diseases the carrier has had, used for immunity.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public HashSet<ProtoId<DiseasePrototype>> PastDiseases = new();

    /// <summary>
    /// All the diseases the carrier has or has had.
    /// Checked against when trying to add a disease
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public IReadOnlyList<ProtoId<DiseasePrototype>> AllDiseases => PastDiseases.Concat(Diseases.Select(x=>(ProtoId<DiseasePrototype>)x.ID)).ToList();

    /// <summary>
    /// A list of diseases which the entity does not
    /// exhibit direct symptoms from. They still transmit
    /// these diseases, just without symptoms.
    /// </summary>
    [DataField("carrierDiseases")]
    public HashSet<ProtoId<DiseasePrototype>>? CarrierDiseases;

    /// <summary>
    /// When this component is initialized,
    /// these diseases will be added to past diseases,
    /// rendering them immune.
    /// </summary>
    [DataField("naturalImmunities")]
    public HashSet<ProtoId<DiseasePrototype>>? NaturalImmunities;
}
