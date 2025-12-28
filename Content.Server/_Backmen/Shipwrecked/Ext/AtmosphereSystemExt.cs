using Content.Server.Atmos.Components;
using Content.Shared.Atmos;
using Robust.Shared.Map.Components;


// ReSharper disable once CheckNamespace
namespace Content.Server.Atmos.EntitySystems;

public sealed partial class AtmosphereSystem
{
    /// <summary>
    /// This is a hack to get a grid that has landed on a planet to conform with a planet's atmosphere.
    /// Remove or replace this when that works properly.
    /// </summary>
    // It may be a very, very long time until that day comes:
    // https://github.com/space-wizards/space-station-14/issues/15652
    public void PatchGridToPlanet(EntityUid grid, GasMixture planetmos)
    {
        if (!TryComp<GridAtmosphereComponent>(grid, out var gridAtmosphereComponent))
            return;

        if (!TryComp<MapGridComponent>(grid, out var mapGridComponent))
            return;

        var enumerator = mapGridComponent.GetAllTilesEnumerator(false);

        while (enumerator.MoveNext(out var tileRef))
        {
            if (tileRef is not {} tile)
                continue;

            // Manually assign an atmosphere to every tile that doesn't have one.
            //
            // The normal API won't let you just assign values;
            // you have to involve adjacent gasses, which is a mess.
            if (!gridAtmosphereComponent.Tiles.TryGetValue(tile.GridIndices, out var tileAir))
            {
                var moles = new float[Atmospherics.AdjustedNumberOfGases];
                planetmos.Moles.CopyTo(moles.AsSpan());

                var tileAtmos = new TileAtmosphere(grid, tile.GridIndices,
                    new GasMixture(planetmos.Volume)
                    {
                        Temperature = planetmos.Temperature,
                        Moles = moles,
                    }, true);
                tileAtmos.Space = false;

                gridAtmosphereComponent.Tiles[tile.GridIndices] = tileAtmos;
            }
            // Then fix all the relative-spacings (very low pressure).
            else if (tileAir.Air?.Pressure <= 3f)
            {
                planetmos.Moles.CopyTo(tileAir.Air.Moles.AsSpan());
                tileAir.Air.Temperature = planetmos.Temperature;
                tileAir.Space = false;
            }
            // Then fix all actual space tiles.
            else if (tileAir.Space)// || tileAir.Air == null)
            {
                var moles = new float[Atmospherics.AdjustedNumberOfGases];
                planetmos.Moles.CopyTo(moles.AsSpan());

                tileAir.Air = new GasMixture(planetmos.Volume)
                {
                    Temperature = planetmos.Temperature,
                    Moles = moles,
                };
                tileAir.Space = false;
                // Make this tile immutable so it can continue to be a source of atmosphere for the grid.
                tileAir.Air.MarkImmutable();
            }
            else
            {
                continue;
            }

            gridAtmosphereComponent.InvalidatedCoords.Add(tile.GridIndices);
        }
    }

    /// <summary>
    /// Hello space.
    /// </summary>
    public void UnpatchGridFromPlanet(EntityUid grid)
    {
        if (!TryComp<GridAtmosphereComponent>(grid, out var gridAtmosphereComponent))
            return;

        if (!TryComp<MapGridComponent>(grid, out var mapGridComponent))
            return;

        var enumerator = mapGridComponent.GetAllTilesEnumerator(false);

        // Lattice tiles eat air for breakfast. They are space.
        var latticeId = _tileDefinitionManager["Lattice"].TileId;

        while (enumerator.MoveNext(out var tileRef))
        {
            if (tileRef is not {} tile)
                continue;

            if (tile.Tile.TypeId != latticeId)
                continue;

            if (!gridAtmosphereComponent.Tiles.TryGetValue(tile.GridIndices, out var tileAir))
                continue;

            tileAir.Space = true;
            gridAtmosphereComponent.InvalidatedCoords.Add(tile.GridIndices);
        }
    }
}
