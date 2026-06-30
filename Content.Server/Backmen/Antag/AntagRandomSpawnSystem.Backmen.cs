using System.Diagnostics.CodeAnalysis;
using Content.Server.Antag.Components;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server.Antag;

public sealed partial class AntagRandomSpawnSystem
{
    partial void InitializeAntagRandomSpawnBackmen()
    {
        SubscribeLocalEvent<AntagRandomSpawnComponent, AfterAntagEntitySelectedEvent>(OnAfterAntagSelected);
    }

    private bool TrySelectLocationBackmen(Entity<AntagRandomSpawnComponent> ent, ref AntagSelectLocationEvent args)
    {
        if (!TryGetSpawnCoordsBackmen(ent.Comp, out var coords))
            return false;

        args.Coordinates.Add(_transform.ToMapCoordinates(coords));

        if (ent.Comp.Coords == null)
            CacheCoordsFromSpawnerAfterSpawnBackmen(ent.Owner);

        return true;
    }

    private void OnAfterAntagSelected(Entity<AntagRandomSpawnComponent> ent, ref AfterAntagEntitySelectedEvent args)
    {
        ent.Comp.Coords ??= Transform(args.EntityUid).Coordinates;
    }

    private bool TryGetSpawnCoordsBackmen(AntagRandomSpawnComponent comp, [NotNullWhen(true)] out EntityCoordinates coords)
    {
        if (comp.Coords != null)
        {
            coords = comp.Coords.Value;
            return true;
        }

        return TryFindRandomTile(out _, out _, out _, out coords);
    }

    private void CacheCoordsFromSpawnerAfterSpawnBackmen(EntityUid rule)
    {
        // SpawnGhostRole assigns the rule after spawning; defer until outside the location event chain.
        Timer.Spawn(0, () =>
        {
            if (!Exists(rule) || !TryComp<AntagRandomSpawnComponent>(rule, out var spawn) || spawn.Coords != null)
                return;

            var query = EntityQueryEnumerator<GhostRoleAntagSpawnerComponent, TransformComponent>();
            while (query.MoveNext(out _, out var spawner, out var xform))
            {
                if (spawner.Rule != rule)
                    continue;

                spawn.Coords = xform.Coordinates;
                return;
            }
        });
    }
}
