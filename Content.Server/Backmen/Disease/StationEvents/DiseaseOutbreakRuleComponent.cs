using Content.Shared.Backmen.Disease;
using Robust.Shared.Prototypes;

namespace Content.Server.Backmen.Disease.StationEvents;

[RegisterComponent]
public sealed partial class DiseaseOutbreakRuleComponent : Component
{
    /// <summary>
    /// Disease prototypes I decided were not too deadly for a random event
    /// </summary>
    /// <remarks>
    /// Fire name
    /// </remarks>
    [DataField("notTooSeriousDiseases")]
    public List<ProtoId<DiseasePrototype>> NotTooSeriousDiseases = new()
    {
        "SpaceCold",
        "VanAusdallsRobovirus",
        "VentCough",
        "AMIV",
        "SpaceFlu",
        "BirdFlew",
        "TongueTwister"
    };
}
