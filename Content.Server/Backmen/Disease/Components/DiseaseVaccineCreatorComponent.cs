using Content.Server.Backmen.Disease.Server;
using Content.Shared.Construction.Prototypes;
using Robust.Shared.Audio;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Server.Backmen.Disease.Components;

[RegisterComponent]
public sealed partial class DiseaseVaccineCreatorComponent : Component
{
    public DiseaseServerComponent? DiseaseServer = null;

    /// <summary>
    /// Biomass cost per vaccine, scaled off of the machine part. (So T1 parts effectively reduce the default to 4.)
    /// Reduced by the part rating.
    /// </summary>
    [DataField("BaseBiomassCost")]
    public int BaseBiomassCost = 5;

    /// <summary>
    /// Current biomass cost, derived from the above.
    /// </summary>
    public int BiomassCost = 4;

    /// <summary>
    /// Current vaccines queued.
    /// </summary>
    public int Queued = 0;

    [DataField("runningSound")]
    public SoundSpecifier RunningSoundPath = new SoundPathSpecifier("/Audio/Machines/vaccinator_running.ogg");
}
