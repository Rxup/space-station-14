using Content.Client.Backmen.Sponsors;
using Content.Corvax.Interfaces.Client;
using Content.Corvax.Interfaces.Shared;
using Content.Shared.Backmen.WL;
using Content.Shared.Species.Components;
using Robust.Shared.Network;

namespace Content.Client.Backmen.WL;

public sealed class WhitelistSystem  : SharedWhitelistSystem
{
    [Dependency] private readonly ISharedSponsorsManager _sponsorsManager = default!;

    public override void Initialize()
    {
        base.Initialize();
    }

    public override void ProcessReform(EntityUid child, Entity<ReformComponent> source)
    {
        // noop
    }

    public bool Whitelisted => (_sponsorsManager as SponsorsManager)?.Whitelisted ?? false;
}
