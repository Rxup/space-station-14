using Content.Server.Atmos.Components;
using Content.Server.Decals;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Atmos.Reactions;
using Content.Shared.Database;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Atmos.EntitySystems;

public sealed partial class AtmosphereSystem
{
    /*
     Handles Hotspots, which are gas-based tile fires that slowly grow and spread
     to adjacent tiles if conditions are met.

     You can think of a hotspot as a small flame on a tile that
     grows by consuming a fuel and oxidizer from the tile's air,
     with a certain volume and temperature.

     This volume grows bigger and bigger as the fire continues,
     until it effectively engulfs the entire tile, at which point
     it starts spreading to adjacent tiles by radiating heat.
     */

    /// <summary>
    /// Collection of hotspot sounds to play.
    /// </summary>
    private static readonly ProtoId<SoundCollectionPrototype> DefaultHotspotSounds = "AtmosHotspot";

    [Dependency] private readonly DecalSystem _decalSystem = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    /// <summary>
    /// Number of cycles the hotspot system must process before it can play another sound
    /// on a hotspot.
    /// </summary>
    private const int HotspotSoundCooldownCycles = 200;

    /// <summary>
    /// Cooldown counter for hotspot sounds.
    /// </summary>
    private int _hotspotSoundCooldown = 0;

    [ViewVariables(VVAccess.ReadWrite)]
    public SoundSpecifier? HotspotSound = new SoundCollectionSpecifier(DefaultHotspotSounds);

    /// <summary>
    /// Processes a hotspot on a <see cref="TileAtmosphere"/>.
    /// </summary>
    /// <param name="ent">The grid entity that belongs to the tile to process.</param>
    /// <param name="tile">The <see cref="TileAtmosphere"/> to process.</param>
    private void ProcessHotspot(
        Entity<GridAtmosphereComponent, GasTileOverlayComponent, MapGridComponent, TransformComponent> ent,
        TileAtmosphere tile)
    {
        var gridAtmosphere = ent.Comp1;

        if (!tile.Hotspot.Valid)
        {
            gridAtmosphere.HotspotTiles.Remove(tile);
            return;
        }

        AddActiveTile(gridAtmosphere, tile);

        if (!tile.Hotspot.SkippedFirstProcess)
        {
            tile.Hotspot.SkippedFirstProcess = true;
            return;
        }

        if (tile.ExcitedGroup != null)
            ExcitedGroupResetCooldowns(tile.ExcitedGroup);

        // Условие тушения hotspot с учётом Backmen-изменений (hydrogen + hypernoblium suppression)
        if (tile.Hotspot.Temperature < Atmospherics.FireMinimumTemperatureToExist ||
            tile.Hotspot.Volume <= 1f ||
            tile.Air == null ||
            tile.Air.GetMoles(Gas.Oxygen) < 0.5f ||
            (tile.Air.GetMoles(Gas.Plasma) < 0.5f &&
            tile.Air.GetMoles(Gas.Tritium) < 0.5f &&
            tile.Air.GetMoles(Gas.Hydrogen) < 0.5f &&
            tile.Air.GetMoles(Gas.HyperNoblium) > 5f))
        {
            tile.Hotspot = new Hotspot();
            InvalidateVisuals(ent, tile);
            return;
        }

        PerformHotspotExposure(tile);

        var gridUid = ent.Owner;
        var tilePos = tile.GridIndices;

        // Обработка декалей (burnt floor) — из твоей ветки
        var tileDecals = _decalSystem.GetDecalsInRange(gridUid, tilePos);
        var tileBurntDecals = 0;

        foreach (var set in tileDecals)
        {
            if (Array.IndexOf(_burntDecals, set.Decal.Id) == -1)
                continue;

            tileBurntDecals++;
            if (tileBurntDecals > 4)
                break;
        }

        if (tileBurntDecals < 4)
        {
            _decalSystem.TryAddDecal(_burntDecals[_random.Next(_burntDecals.Length)],
                new EntityCoordinates(gridUid, tilePos),
                out _,
                cleanable: true);
        }

        if (tile.Hotspot.Bypassing)
        {
            tile.Hotspot.State = 3;

            if (tile.ExcitedGroup != null)
                ExcitedGroupResetCooldowns(tile.ExcitedGroup);

            // Распространение огня на соседние тайлы (из upstream, но с твоим комментарием)
            if (tile.Air.Temperature > Atmospherics.FireMinimumTemperatureToSpread)
            {
                var radiatedTemperature = tile.Air.Temperature * Atmospherics.FireSpreadRadiosityScale;
                foreach (var otherTile in tile.AdjacentTiles)
                {
                    if (otherTile == null || otherTile.Hotspot.Valid)
                        continue;

                    HotspotExpose(gridAtmosphere, otherTile, radiatedTemperature, Atmospherics.CellVolume / 4);
                }
            }
        }
        else
        {
            // Маленький огонь
            tile.Hotspot.State = (byte)(tile.Hotspot.Volume > Atmospherics.CellVolume * 0.4f ? 2 : 1);
        }

        // Определение Bypassing с твоим изменением (backmen: gas)
        tile.Hotspot.Bypassing = tile.Hotspot.SkippedFirstProcess && tile.Hotspot.Volume > tile.Air.Volume * 0.95f;

        if (tile.Hotspot.Temperature > tile.MaxFireTemperatureSustained)
            tile.MaxFireTemperatureSustained = tile.Hotspot.Temperature;

        // Звук огня
        if (_hotspotSoundCooldown++ == 0 && HotspotSound != null)
        {
            var coordinates = _mapSystem.ToCenterCoordinates(tile.GridIndex, tile.GridIndices);
            _audio.PlayPvs(HotspotSound, coordinates,
                HotspotSound.Params.WithVariation(0.15f / tile.Hotspot.State).WithVolume(-5f + 5f * tile.Hotspot.State));
        }

        if (_hotspotSoundCooldown > HotspotSoundCooldownCycles)
            _hotspotSoundCooldown = 0;
    }
    /// <summary>
    /// Exposes a tile to a hotspot of given temperature and volume, igniting it if conditions are met.
    /// </summary>
    /// <param name="gridAtmosphere">The <see cref="GridAtmosphereComponent"/> of the grid the tile is on.</param>
    /// <param name="tile">The <see cref="TileAtmosphere"/> to expose.</param>
    /// <param name="exposedTemperature">The temperature of the hotspot to expose.
    /// You can think of this as exposing a temperature of a flame.</param>
    /// <param name="exposedVolume">The volume of the hotspot to expose.
    /// You can think of this as how big the flame is initially.
    /// Bigger flames will ramp a fire faster.</param>
    /// <param name="soh">Whether to "boost" a fire that's currently on the tile already.
    /// Does nothing if the tile isn't already a hotspot.
    /// This clamps the temperature and volume of the hotspot to the maximum
    /// of the provided parameters and whatever's on the tile.</param>
    /// <param name="sparkSourceUid">Entity that started the exposure for admin logging.</param>
    private void HotspotExpose(GridAtmosphereComponent gridAtmosphere,
        TileAtmosphere tile,
        float exposedTemperature,
        float exposedVolume,
        bool soh = false,
        EntityUid? sparkSourceUid = null)
    {
        if (tile.Air == null)
            return;

        var oxygen = tile.Air.GetMoles(Gas.Oxygen);
        if (oxygen < 0.5f)
            return;

        var plasma = tile.Air.GetMoles(Gas.Plasma);
        var tritium = tile.Air.GetMoles(Gas.Tritium);
        var hydrogen = tile.Air.GetMoles(Gas.Hydrogen);       // backmen: gas
        var hypernoblium = tile.Air.GetMoles(Gas.HyperNoblium); // backmen: gas

        if (tile.Hotspot.Valid)
        {
            if (soh)
            {
                // Усиление существующего огня только если есть топливо и нет супрессора
                if ((plasma > 0.5f || tritium > 0.5f || hydrogen > 0.5f) && hypernoblium < 5f)
                {
                    tile.Hotspot.Temperature = MathF.Max(tile.Hotspot.Temperature, exposedTemperature);
                    tile.Hotspot.Volume = MathF.Max(tile.Hotspot.Volume, exposedVolume);
                }
            }
            return;
        }

        // Зажигание нового hotspot — только если есть топливо и hypernoblium < 5
        if (exposedTemperature > Atmospherics.PlasmaMinimumBurnTemperature &&
            (plasma > 0.5f || tritium > 0.5f || hydrogen > 0.5f) &&
            hypernoblium < 5f)
        {
            if (sparkSourceUid.HasValue)
            {
                _adminLog.Add(LogType.Flammable, LogImpact.High,
                    $"Heat/spark of {ToPrettyString(sparkSourceUid.Value)} caused atmos ignition of gas: " +
                    $"{tile.Air.Temperature:0.##}K - {oxygen}mol Oxygen, {plasma}mol Plasma, {tritium}mol Tritium, {hydrogen}mol Hydrogen");
            }

            tile.Hotspot = new Hotspot
            {
                Volume = exposedVolume * 25f,
                Temperature = exposedTemperature,
                SkippedFirstProcess = tile.CurrentCycle > gridAtmosphere.UpdateCounter,
                Valid = true,
                State = 1
            };

            AddActiveTile(gridAtmosphere, tile);
            gridAtmosphere.HotspotTiles.Add(tile);
        }
    }

    /// <summary>
    /// Performs hotspot exposure processing on a <see cref="TileAtmosphere"/>.
    /// </summary>
    /// <param name="tile">The <see cref="TileAtmosphere"/> to process.</param>
    private void PerformHotspotExposure(TileAtmosphere tile)
    {
        if (tile.Air == null || !tile.Hotspot.Valid)
            return;

        // Determine if the tile has become a full-blown fire if the volume of the fire has effectively reached
        // the volume of the tile's air.
        tile.Hotspot.Bypassing = tile.Hotspot.SkippedFirstProcess && tile.Hotspot.Volume > tile.Air.Volume * 0.95f;

        // If the tile is effectively a full fire, use the tile's air for reactions, don't bother partitioning.
        if (tile.Hotspot.Bypassing)
        {
            tile.Hotspot.Volume = tile.Air.ReactionResults[(byte)GasReaction.Fire] * Atmospherics.FireGrowthRate;
            tile.Hotspot.Temperature = tile.Air.Temperature;
        }
        // Otherwise, pull out a fraction of the tile's air (the current hotspot volume) to perform reactions on.
        else
        {
            var affected = tile.Air.RemoveVolume(tile.Hotspot.Volume);
            affected.Temperature = tile.Hotspot.Temperature;
            React(affected, tile);
            tile.Hotspot.Temperature = affected.Temperature;
            // Scale the fire based on the type of reaction that occured.
            tile.Hotspot.Volume = affected.ReactionResults[(byte)GasReaction.Fire] * Atmospherics.FireGrowthRate;
            Merge(tile.Air, affected);
        }

        var fireEvent = new TileFireEvent(tile.Hotspot.Temperature, tile.Hotspot.Volume);
        _entSet.Clear();
        _lookup.GetLocalEntitiesIntersecting(tile.GridIndex, tile.GridIndices, _entSet, 0f);

        foreach (var entity in _entSet)
        {
            RaiseLocalEvent(entity, ref fireEvent);
        }
    }
}
