using Content.Server.GameTicking;
using Content.Server.Ghost.Roles;
using Content.Server.Ghost.Roles.Components;
using Robust.Shared.Console;

namespace Content.Server.Backmen.Arrivals;

public sealed partial class AutoRespawnSystem : EntitySystem
{
    [Dependency] private GameTicker _ticker = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BkmRespawnerComponent, TakeGhostRoleEvent>(OnRequestRespawn, before: new []{ typeof(GhostRoleSystem) });
    }

    private void OnRequestRespawn(Entity<BkmRespawnerComponent> ent, ref TakeGhostRoleEvent args)
    {
        args.TookRole = true;
        _ticker.Respawn(args.Player);
    }
}
