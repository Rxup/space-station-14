using Content.Shared.Roles;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.List;

namespace Content.Server.Backmen.Loadout;

[Prototype("loadout")]
public sealed class LoadoutItemPrototype : IPrototype
{
    [IdDataField] public string ID { get; } = default!;

    [DataField("entity", required: true)]
    public ProtoId<EntityPrototype> EntityId { get; } = default!;

    // Corvax-Sponsors-Start
    [DataField("sponsorOnly")]
    public bool SponsorOnly = false;
    // Corvax-Sponsors-End

    [DataField("whitelistJobs")]
    public List<ProtoId<JobPrototype>>? WhitelistJobs { get; }

    [DataField("blacklistJobs")]
    public List<ProtoId<JobPrototype>>? BlacklistJobs { get; }

    [DataField("speciesRestriction")]
    public List<string>? SpeciesRestrictions { get; }
}
