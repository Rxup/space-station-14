using Content.Shared.Backmen.WL;
using Content.Shared.Species.Components;
using Robust.Shared.Network;

namespace Content.Client.Backmen.WL;

public sealed class WhitelistSystem  : SharedWhitelistSystem
{
    public override void Initialize()
    {
        base.Initialize();
    }

    public override void ProcessReform(EntityUid child, Entity<ReformComponent> source)
    {
        // noop
    }

    public bool Whitelisted { get; internal set; }
}
