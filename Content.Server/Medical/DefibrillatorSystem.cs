using Content.Server.EUI;
using Content.Server.Ghost;
using Content.Server.Revenant.Components;
using Content.Shared.Medical;
using Content.Shared.Mind;
using Robust.Shared.Player;

namespace Content.Server.Medical;

public sealed partial class DefibrillatorSystem : SharedDefibrillatorSystem
{
    [Dependency] private EuiManager _eui = default!;
    [Dependency] private ISharedPlayerManager _player = default!;
    [Dependency] private SharedMindSystem _mind = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<EssenceComponent, TargetDefibrillatedEvent>(OnTargetDefibrillated);
    }

    private void OnTargetDefibrillated(Entity<EssenceComponent> ent, ref TargetDefibrillatedEvent args)
    {
        // start-backmen: revenant
        ent.Comp.Harvested = false;
        // end-backmen: revenant
    }

    protected override void OpenReturnToBodyEui(Entity<MindComponent> mind, ICommonSession session)
    {
        _eui.OpenEui(new ReturnToBodyEui(mind, _mind, _player), session);
    }
}
