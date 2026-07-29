using System.Linq;
using System.Numerics;
using Content.Server._Lavaland.Procedural.Components;
using Content.Shared._Lavaland.Procedural.Components;
using Content.Shared._Lavaland.Procedural.Prototypes;
using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;
using Content.Shared.Maps;
using Robust.Server.Physics;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Random;

namespace Content.Server._Lavaland.Procedural.Systems;

public sealed partial class LavalandPlanetSystem
{
        [Dependency] private GridFixtureSystem _gridFixture = default!;
        [Dependency] private ITileDefinitionManager _tileDef = default!;
        [Dependency] private SharedRoofSystem _roofSystem = default!;
        [Dependency] private TileSystem _tileSystem = default!;

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

            if (!TryComp<MapGridComponent>(spawned.Value, out var boundGrid))
            {
                Log.Error($"Failed to load ruin {ruin.ID} onto dummy map, it doesn't have MapGrid! AAAAA!!");
                Del(spawned);
                return false;
            }

            // Tile LocalAABB is relative to grid origin — more reliable than fixture AABBs
            // which can miss coverage and let ruins overlap.
            var calculatedBox = boundGrid.LocalAABB.Enlarged(8f);
            ruinsBoundsDict.Add(ruin.ID, calculatedBox);

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
            coords.Remove(coord);
            if (spawned != null)
                Del(spawned.Value);
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
        if (HasComp<MapGridComponent>(lavaland.Owner) && !ruin.IsGrid)
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

                    // GridFixtureSystem.Merge copies tiles but not TileHistory, so stack planet
                    // under isSpace tiles (Lattice) afterwards via ReplaceTile.
                    // Resolve the under-tile before merge: after merge the cell is already Lattice,
                    // and TryGetBiomeTile would prefer that existing tile over the biome.
                    var spaceTiles = new List<(Vector2i PlanetIndices, Tile SpaceTile, Tile UnderTile)>();
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

                            var ruinTile = tileRef.Value.Tile;
                            if (ruinTile.IsEmpty)
                                continue;

                            var tileDef = (ContentTileDefinition) _tileDef[ruinTile.TypeId];
                            if (!tileDef.MapAtmosphere)
                                continue;

                            Tile? underTile = null;
                            if (_map.TryGetTileRef(sourceGridUid, sourceGrid, offsetTile, out var existingRef) &&
                                !existingRef.Tile.IsEmpty)
                            {
                                var existingDef = (ContentTileDefinition) _tileDef[existingRef.Tile.TypeId];
                                if (!existingDef.MapAtmosphere)
                                    underTile = existingRef.Tile;
                            }

                            if (underTile == null &&
                                _biome.TryGetBiomeTile(sourceGridUid, sourceGrid, offsetTile, out var biomeTile))
                            {
                                underTile = biomeTile;
                            }

                            if (underTile != null)
                                spaceTiles.Add((offsetTile, ruinTile, underTile.Value));
                        }
                    }

                    _gridFixture.Merge(sourceGridUid, spawned.Value, matrix);

                    foreach (var (planetIndices, spaceTile, underTile) in spaceTiles)
                    {
                        var spaceDef = (ContentTileDefinition) _tileDef[spaceTile.TypeId];
                        _map.SetTile(sourceGridUid, sourceGrid, planetIndices, underTile);
                        var tileRef = _map.GetTileRef(sourceGridUid, sourceGrid, planetIndices);
                        _tileSystem.ReplaceTile(tileRef, spaceDef, sourceGridUid, sourceGrid, variant: spaceTile.Variant);
                    }

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
