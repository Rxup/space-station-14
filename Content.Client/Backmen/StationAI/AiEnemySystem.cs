using Content.Shared.Backmen.StationAI;
using Content.Shared.Backmen.StationAI.Components;
using Content.Shared.Ghost;
using Content.Shared.StatusIcon;
using Content.Shared.StatusIcon.Components;
using Robust.Client.Player;
using Robust.Shared.Prototypes;

namespace Content.Client.Backmen.StationAI;

public sealed class AiEnemySystem : SharedAiEnemySystem
{
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    private EntityQuery<GhostComponent> _ghostQuery;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AIEnemyNTComponent, GetStatusIconsEvent>(GetIcon);
        _ghostQuery = GetEntityQuery<GhostComponent>();
    }

    [ValidatePrototypeId<SecurityIconPrototype>]
    private const string AiEnemyStatus = "AiIconEnemyTarget";
    private void GetIcon(Entity<AIEnemyNTComponent> target, ref GetStatusIconsEvent args)
    {
        var ent = _player.LocalSession?.AttachedEntity ?? EntityUid.Invalid;

        if (!EntityQuery.HasComp(ent) && !_ghostQuery.HasComp(ent))
            return;
        args.StatusIcons.Add(_prototype.Index<FactionIconPrototype>(AiEnemyStatus));
    }
}
