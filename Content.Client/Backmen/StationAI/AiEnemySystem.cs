using Content.Shared.Backmen.StationAI;
using Content.Shared.Backmen.StationAI.Components;
using Content.Shared.Backmen.StationAI.Systems;
using Content.Shared.Ghost;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.StatusIcon;
using Content.Shared.StatusIcon.Components;
using Robust.Client.Player;
using Robust.Shared.Prototypes;

namespace Content.Client.Backmen.StationAI;

public sealed class AiEnemySystem : SharedAiEnemySystem
{
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AIEnemyNTComponent, GetStatusIconsEvent>(GetIcon);
    }

    protected override void ToggleEnemy(EntityUid u, EntityUid target)
    {
        //noop
    }

    [ValidatePrototypeId<StatusIconPrototype>]
    private const string AiEnemyStatus = "AiIconEnemyTarget";
    private void GetIcon(Entity<AIEnemyNTComponent> target, ref GetStatusIconsEvent args)
    {
        var ent = _player.LocalSession?.AttachedEntity ?? EntityUid.Invalid;

        if(args.InContainer)
            return;

        if (!(EntityQuery.HasComponent(ent) || HasComp<GhostComponent>(ent)))
        {
            return;
        }
        args.StatusIcons.Add(_prototype.Index<StatusIconPrototype>(AiEnemyStatus));
    }
}
