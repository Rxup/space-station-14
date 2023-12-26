using Content.Server.Backmen.Species.Shadowkin.Components;
using Content.Shared.Backmen.Species.Shadowkin.Events;

namespace Content.Server.Backmen.Species.Shadowkin.Systems;

public sealed class ShadowkinBlackeyeTraitSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ShadowkinBlackeyeTraitComponent, ComponentStartup>(OnStartup);
    }

    private void OnStartup(EntityUid uid, ShadowkinBlackeyeTraitComponent _, ComponentStartup args)
    {
        var net = GetNetEntity(uid);
        RaiseLocalEvent(uid, new ShadowkinBlackeyeEvent(net, false));
        RaiseNetworkEvent(new ShadowkinBlackeyeEvent(net, false));
    }
}
