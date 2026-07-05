using Content.Shared.Backmen.Surgery.Traumas;
using Content.Shared.Body;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Backmen.Medical;

// start-backmen: organ-damage-alerts
[Serializable, NetSerializable]
public struct HealthAnalyzerOrganAlert
{
    public ProtoId<OrganCategoryPrototype> Category;
    public OrganSeverity Severity;
}
// end-backmen: organ-damage-alerts
