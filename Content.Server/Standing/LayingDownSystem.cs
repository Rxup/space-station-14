using Content.Shared.CCVar;
using Content.Shared.Backmen.Standing;
using Robust.Shared.Configuration;

namespace Content.Server.Standing;

public sealed class LayingDownSystem : SharedLayingDownSystem // WD EDIT
{
    [Dependency] private readonly INetConfigurationManager _cfg = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<CheckAutoGetUpEvent>(OnCheckAutoGetUp);
    }

    private void OnCheckAutoGetUp(CheckAutoGetUpEvent ev, EntitySessionEventArgs args)
    {
        var uid = GetEntity(ev.User);

        if (!TryComp(uid, out LayingDownComponent? layingDown))
            return;

        layingDown.AutoGetUp = _cfg.GetClientCVar(args.SenderSession.Channel, CCVars.AutoGetUp);
        Dirty(uid, layingDown);
    }
}
