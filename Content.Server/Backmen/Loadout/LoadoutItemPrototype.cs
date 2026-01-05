using Content.Shared.Roles;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.List;

namespace Content.Server.Backmen.Loadout;

[Prototype("bkmloadout")]
public sealed partial class LoadoutItemPrototype : IPrototype
{
    [IdDataField] public string ID { get; private set; } = default!;

    [DataField("entity", required: true)]
    public EntProtoId EntityId { get; private set; } = default!;

    // Corvax-Sponsors-Start
    [DataField("sponsorOnly")]
    public bool SponsorOnly = false;
    // Corvax-Sponsors-End

    [DataField("whitelistJobs")]
    public List<ProtoId<JobPrototype>>? WhitelistJobs { get; private set; }

    [DataField("blacklistJobs")]
    public List<ProtoId<JobPrototype>>? BlacklistJobs { get; private set; }

    [DataField("speciesRestriction")]
    public List<string>? SpeciesRestrictions { get; private set; }
}
