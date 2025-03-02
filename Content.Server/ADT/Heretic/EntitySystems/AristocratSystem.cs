using Content.Server.Atmos.EntitySystems;
using Content.Server.Heretic.Components;
using Content.Server.Temperature.Components;
using Content.Server.Temperature.Systems;
using Content.Shared.Heretic;
using Content.Shared.Maps;
using Content.Shared.Speech.Muting;
using Content.Shared.StatusEffect;
using Content.Shared.Tag;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using System.Linq;
using System.Numerics;
using Robust.Server.GameObjects;

namespace Content.Server.Heretic.EntitySystems;

// void path heretic exclusive
public sealed partial class AristocratSystem : EntitySystem
{
    [Dependency] private readonly TileSystem _tile = default!;
    [Dependency] private readonly IRobustRandom _rand = default!;
    [Dependency] private readonly IPrototypeManager _prot = default!;
    [Dependency] private readonly AtmosphereSystem _atmos = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly TemperatureSystem _temp = default!;
    [Dependency] private readonly StatusEffectsSystem _statusEffect = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly MapSystem _map = default!;

    [ValidatePrototypeId<EntityPrototype>]
    private const string SnowWallPrototype = "WallSnowCobblebrick";

    public override void Initialize()
    {
        base.Initialize();
        _ghoulQuery = GetEntityQuery<GhoulComponent>();
        _hereticQuery = GetEntityQuery<HereticComponent>();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<AristocratComponent>();

        while (query.MoveNext(out var owner, out var aristocrat))
        {
            aristocrat.UpdateTimer += frameTime;

            if (aristocrat.UpdateTimer < aristocrat.UpdateDelay)
                continue;

            Cycle((owner, aristocrat));
            aristocrat.UpdateTimer = 0;
        }
    }

    private HashSet<EntityUid> _cycleEntities = new();
    private EntityQuery<HereticComponent> _hereticQuery;
    private EntityQuery<GhoulComponent> _ghoulQuery;

    private void Cycle(Entity<AristocratComponent> ent)
    {
        SpawnTiles(ent);
        var entTransform = Transform(ent);

        var mix = _atmos.GetTileMixture((ent, entTransform));
        if (mix != null)
            mix.Temperature -= 50f;

        var queryTemp = GetEntityQuery<TemperatureComponent>();

        // replace certain things with their winter analogue
        _cycleEntities.Clear();
        _lookup.GetEntitiesInRange(entTransform.Coordinates, ent.Comp.Range, _cycleEntities);
        foreach (var look in _cycleEntities)
        {
            if (_hereticQuery.HasComp(look) || _ghoulQuery.HasComp(look))
                continue;

            if (queryTemp.TryComp(look, out var temp))
                _temp.ChangeHeat(look, -100f, true, temp);

            _statusEffect.TryAddStatusEffect<MutedComponent>(look, "Muted", TimeSpan.FromSeconds(5), true);

            if (!_rand.Prob(.45f) || !_tag.HasTag(look, "Wall") || Prototype(look)?.ID == SnowWallPrototype)
                continue;

            // replace walls with snow ones
            Spawn(SnowWallPrototype, Transform(look).Coordinates);
            QueueDel(look);
        }
    }

    private void SpawnTiles(Entity<AristocratComponent> ent)
    {
        var xform = Transform(ent);

        if (!TryComp<MapGridComponent>(xform.GridUid, out var grid))
            return;

        var pos = xform.Coordinates.Position;
        var box = new Box2(pos + new Vector2(-ent.Comp.Range, -ent.Comp.Range),
            pos + new Vector2(ent.Comp.Range, ent.Comp.Range));
        var tileRefs = _map.GetLocalTilesIntersecting(xform.GridUid.Value, grid, box).ToList();

        if (tileRefs.Count == 0)
            return;

        var tiles = new List<TileRef>();
        var tiles2 = new List<TileRef>();
        foreach (var tile in tileRefs)
        {
            if (_rand.Prob(.45f))
                tiles.Add(tile);

            if (_rand.Prob(.05f))
                tiles2.Add(tile);
        }

        foreach (var tileRef in tiles)
        {
            var tile = _prot.Index<ContentTileDefinition>("FloorAstroSnow");
            _tile.ReplaceTile(tileRef, tile);
        }

        // foreach (var tileRef in tiles2)
        // {
        //     // todo add more tile variety
        // }
    }
}
