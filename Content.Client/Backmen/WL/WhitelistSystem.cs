using Content.Corvax.Interfaces.Client;
using Content.Shared.Backmen.WL;
using Content.Shared.Species.Components;
using Robust.Shared.Network;

namespace Content.Client.Backmen.WL;

public sealed class WhitelistSystem  : SharedWhitelistSystem
{
    [Dependency] private readonly IClientSponsorsManager _sponsorsManager = default!;

    public override void Initialize()
    {
        base.Initialize();
    }

    public override void ProcessReform(EntityUid child, Entity<ReformComponent> source)
    {
        // noop
    }

    public bool Whitelisted => _sponsorsManager.Whitelisted;
}
