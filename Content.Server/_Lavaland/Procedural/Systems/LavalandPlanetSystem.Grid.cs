using System;
using System.Linq;
using System.Numerics;
using Content.Server._Lavaland.Procedural.Components;
using Content.Server.Atmos;
using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Light.Components;
using Content.Shared._Lavaland.Procedural.Components;
using Content.Shared._Lavaland.Procedural.Prototypes;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;
using Content.Shared.Maps;
using Robust.Server.Physics;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Server._Lavaland.Procedural.Systems;

public sealed partial class LavalandPlanetSystem
{
        [Dependency] private readonly GridFixtureSystem _gridFixture = default!;
        [Dependency] private readonly ITileDefinitionManager _tileDef = default!;
        [Dependency] private readonly SharedRoofSystem _roofSystem = default!;

    private bool LoadGridRuin(
        LavalandGridRuinPrototype ruin,
        Entity<LavalandMapComponent> lavaland,
        Entity<LavalandPreloaderComponent> preloader,
        Random random,
        ref Dictionary<string, Box2> ruinsBoundsDict,
        ref List<Box2> usedSpace,
        ref List<Vector2> coords)
    {
        EntityUid? spawned = null;
        if (coords.Count == 0)
            return false;

        var coord = random.Pick(coords);
        var mapXform = Transform(preloader);
        Box2 ruinBox; // This is ruin box, but moved to it's correct coords on the map

        // Check if we already calculated that boundary before, and if we didn't then calculate it now
        if (!ruinsBoundsDict.TryGetValue(ruin.ID, out var box))
        {
            if (!_mapLoader.TryLoadGrid(mapXform.MapID, ruin.Path, out var spawnedBoundedGrid))
            {
                Log.Error($"Failed to load ruin {ruin.ID} onto dummy map, on stage of loading! AAAAA!!");
                return false;
            }

            // It's not useless!
            spawned = spawnedBoundedGrid.Value.Owner;

            if (!_fixtureQuery.TryGetComponent(spawned, out var manager))
            {
                Log.Error($"Failed to load ruin {ruin.ID} onto dummy map, it doesn't have fixture component! AAAAA!!");
                Del(spawned);
                return false;
            }

            // Actually calculate ruin bound
            var transform = _physics.GetRelativePhysicsTransform(spawned.Value, preloader.Owner);
            // holy shit
            var bounds = (from fixture in manager.Fixtures.Values where fixture.Hard select fixture.Shape.ComputeAABB(transform, 0).Rounded(0)).ToList();
            // Round this list of boxes up to
            var calculatedBox = _random.Pick(bounds);
            foreach (var bound in bounds)
            {
                calculatedBox = calculatedBox.Union(bound);
            }

            // Safety measure
            calculatedBox = calculatedBox.Enlarged(8f);

            // Add calculated box to dictionary
            ruinsBoundsDict.Add(ruin.ID, calculatedBox);

            // Move our calculated box to correct position
            var v1 = calculatedBox.BottomLeft + coord;
            var v2 = calculatedBox.TopRight + coord;
            ruinBox = new Box2(v1, v2);

            // Teleport it into place on preloader map
            _transform.SetCoordinates(spawned.Value, new EntityCoordinates(preloader, coord));
        }
        else
        {
            // Why there's no method to move the Box2 around???
            var v1 = box.BottomLeft + coord;
            var v2 = box.TopRight + coord;
            ruinBox = new Box2(v1, v2);
        }

        // If any used boundary intersects with current boundary, return
        if (usedSpace.Any(used => used.Intersects(ruinBox)))
        {
            Log.Debug("Ruin can't be placed on it's coordinates, skipping spawn");
            return false;
        }

        // Try to load it on a dummy map if it wasn't already
        if (spawned == null)
        {
            if (!_mapLoader.TryLoadGrid(mapXform.MapID, ruin.Path, out var spawnedGrid, offset: coord))
            {
                Log.Error($"Failed to load ruin {ruin.ID} onto dummy map, on stage of reparenting it to Lavaland! (this is really bad)");
                return false;
            }

            spawned = spawnedGrid.Value.Owner;
        }

        // Set its position to Lavaland
        var spawnedXForm = _xformQuery.GetComponent(spawned.Value);
        _metaData.SetEntityName(spawned.Value, Loc.GetString(ruin.Name));
        _transform.SetParent(spawned.Value, spawnedXForm, lavaland);
        _transform.SetCoordinates(spawned.Value, new EntityCoordinates(lavaland, spawnedXForm.Coordinates.Position.Rounded()));

        // Merge fixtures from lavaland grid to spawned ruin grid
        if (HasComp<MapGridComponent>(lavaland.Owner))
        {
            var sourceGridUid = lavaland.Owner;

            if (TryComp<MapGridComponent>(spawned.Value, out var spawnedGrid) &&
                TryComp<MapGridComponent>(sourceGridUid, out var sourceGrid) &&
                sourceGridUid != spawned.Value)
            {
                try
                {
                    // Get the position of source grid (lavaland) in local coordinates of target grid (spawned)
                    var sourceWorldPos = _transform.GetWorldPosition(sourceGridUid);
                    var localPos = _map.WorldToLocal(spawned.Value, spawnedGrid, sourceWorldPos);
                    var offset = (Vector2i)localPos;

                    // Get the rotation of the target grid
                    var rotation = Transform(spawned.Value).LocalRotation;

                    var tilesToRoof = new HashSet<Vector2i>();
                    Entity<MapGridComponent, RoofComponent> spawnedRoof = (spawned.Value, spawnedGrid, EnsureComp<RoofComponent>(spawned.Value));
                    Entity<MapGridComponent?, RoofComponent?> roofMap = (sourceGridUid, sourceGrid, EnsureComp<RoofComponent>(sourceGridUid));

                    var matrix = Matrix3Helpers.CreateTransform(offset, rotation);

                    {
                        var enumerator = _map.GetAllTilesEnumerator(spawned.Value, spawnedGrid);
                        while (enumerator.MoveNext(out var tileRef))
                        {
                            var offsetTile = Vector2.Transform(new Vector2(tileRef.Value.GridIndices.X, tileRef.Value.GridIndices.Y) + sourceGrid.TileSizeHalfVector, matrix)
                                .Floored();
                            if (_roofSystem.IsRooved(spawnedRoof, tileRef.Value.GridIndices))
                            {
                                _roofSystem.SetRoof(roofMap, offsetTile, true);
                                tilesToRoof.Add(offsetTile);
                            }

                            if(tileRef.Value.Tile != Tile.Empty)
                                continue;

                            if (_map.TryGetTileRef(sourceGridUid, sourceGrid, offsetTile, out var lavalandTile) &&
                                !lavalandTile.Tile.IsEmpty)
                            {
                                _map.SetTile(spawned.Value, spawnedGrid, offsetTile, lavalandTile.Tile);
                            }
                        }
                    }

                    _gridFixture.Merge(sourceGridUid, spawned.Value, matrix);
                    
                    foreach (var vector2I in tilesToRoof)
                    {
                        _roofSystem.SetRoof(roofMap, vector2I, true);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"Failed to merge fixtures for ruin {ruin.ID}: {ex}");
                }
            }
        }

        // yaaaaaaaaaaaaaaaay
        usedSpace.Add(ruinBox);
        coords.Remove(coord);
        return true;
    }
}
