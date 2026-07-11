using Robust.Shared.Prototypes;
using Content.Shared.Roles;

namespace Content.Shared.Backmen.Targeting;

[Prototype]
public sealed partial class CombatTargetOddsRulesPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public ProtoId<CombatTargetOddsPrototype> Default = "Default";

    [DataField(required: true)]
    public ProtoId<CombatTargetOddsPrototype> Security = "Security";

    [DataField(required: true)]
    public ProtoId<CombatTargetOddsPrototype> Elite = "Elite";

    [DataField]
    public ProtoId<CombatTargetOddsPrototype> Admin = "Admin";

    [DataField]
    public HashSet<ProtoId<JobPrototype>> SecurityJobs = new();

    [DataField]
    public HashSet<ProtoId<JobPrototype>> EliteJobs = new();

    [DataField]
    public HashSet<EntProtoId> EliteMindRoles = new();

    [DataField]
    public List<EntProtoId> SecurityBorgPrototypes = new();

    [DataField]
    public List<EntProtoId> EliteBorgPrototypes = new();
}
