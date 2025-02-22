using Content.Server.Shuttles.Systems;
using Content.Server.Shuttles.Components;
using Content.Server.Station.Components;
using Content.Server.Cargo.Systems;
using Content.Server.Station.Systems;
using Content.Shared.Backmen.Shipyard;
using Content.Shared.GameTicking;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Content.Shared.Backmen.CCVar;
using Robust.Shared.Configuration;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Content.Shared.Backmen.Shipyard.Components;
using Content.Shared.Backmen.Shipyard.Prototypes;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map.Components;
using Robust.Shared.Utility;

namespace Content.Server.Backmen.Shipyard.Systems;

public sealed partial class ShipyardSystem : SharedShipyardSystem
{
    [Dependency] private readonly IConfigurationManager _configManager = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly PricingSystem _pricing = default!;
    [Dependency] private readonly ShuttleSystem _shuttle = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly MapLoaderSystem _map = default!;
    [Dependency] private readonly ShipyardConsoleSystem _shipyardConsole = default!;
    [Dependency] private readonly MetaDataSystem _metaDataSystem = default!;

    public MapId? ShipyardMap { get; private set; }
    private float _shuttleIndex;
    private const float ShuttleSpawnBuffer = 1f;
    private ISawmill _sawmill = default!;
    private bool _enabled;

    public override void Initialize()
    {
        _enabled = _configManager.GetCVar(CCVars.Shipyard);
        _configManager.OnValueChanged(CCVars.Shipyard, SetShipyardEnabled);
        _sawmill = Logger.GetSawmill("shipyard");
        _shipyardConsole.InitializeConsole();
        SubscribeLocalEvent<ShipyardConsoleComponent, ComponentInit>(OnShipyardStartup);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }

    private void OnShipyardStartup(EntityUid uid, ShipyardConsoleComponent component, ComponentInit args)
    {
        if (!_enabled)
            return;

        SetupShipyard();
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        _configManager.UnsubValueChanged(CCVars.Shipyard, SetShipyardEnabled);
        CleanupShipyard();
    }

    private void SetShipyardEnabled(bool value)
    {
        if (_enabled == value)
            return;

        _enabled = value;

        if (value)
        {
            SetupShipyard();
        }
        else
        {
            CleanupShipyard();
        }
    }

    /// <summary>
    /// Adds a ship to the shipyard, calculates its price, and attempts to ftl-dock it to the given station
    /// </summary>
    /// <param name="stationUid">The ID of the station to dock the shuttle to</param>
    /// <param name="shuttlePath">The path to the shuttle file to load. Must be a grid file!</param>
    public bool TryPurchaseShuttle(EntityUid stationUid, VesselPrototype vessel, [NotNullWhen(true)] out ShuttleComponent? shuttle)
    {
        var shuttlePath = vessel.ShuttlePath.ToString();

        if (!TryComp<StationDataComponent>(stationUid, out var stationData) || !TryAddShuttle(shuttlePath, out var shuttleGrid) || !TryComp<ShuttleComponent>(shuttleGrid, out shuttle))
        {
            shuttle = null;
            return false;
        }

        var price = _pricing.AppraiseGrid((EntityUid) shuttleGrid, null);
        var targetGrid = _station.GetLargestGrid(stationData);


        if (targetGrid == null) //how are we even here with no station grid
        {
            _mapManager.DeleteGrid((EntityUid) shuttleGrid);
            shuttle = null;
            return false;
        }

        _metaDataSystem.SetEntityName(shuttleGrid.Value, vessel.Name);

        _sawmill.Info($"Shuttle {shuttlePath} was purchased at {ToPrettyString((EntityUid) stationUid)} for {price:f2}");
        //can do TryFTLDock later instead if we need to keep the shipyard map paused
        _shuttle.FTLToDock(shuttleGrid.Value, shuttle, targetGrid.Value);
        return true;
    }

    /// <summary>
    /// Loads a shuttle into the ShipyardMap from a file path
    /// </summary>
    /// <param name="shuttlePath">The path to the grid file to load. Must be a grid file!</param>
    /// <returns>Returns the EntityUid of the shuttle</returns>
    private bool TryAddShuttle(string shuttlePath, [NotNullWhen(true)] out EntityUid? shuttleGrid)
    {
        shuttleGrid = null;
        if (ShipyardMap == null)
            return false;

        if (!_map.TryLoadGrid(ShipyardMap.Value, new ResPath(shuttlePath), out var grid, offset: new Vector2(500f + _shuttleIndex, 1f)))
        {
            _sawmill.Error($"Unable to spawn shuttle {shuttlePath}");
            return false;
        }

        _shuttleIndex += Comp<MapGridComponent>(grid.Value).LocalAABB.Width + ShuttleSpawnBuffer;

        shuttleGrid = grid;
        return true;
    }

    private void CleanupShipyard()
    {
        if (ShipyardMap == null || !_mapManager.MapExists(ShipyardMap.Value))
        {
            ShipyardMap = null;
            return;
        }

        _mapManager.DeleteMap(ShipyardMap.Value);
    }

    private void SetupShipyard()
    {
        if (ShipyardMap != null && _mapManager.MapExists(ShipyardMap.Value))
            return;

        ShipyardMap = _mapManager.CreateMap();

        _mapManager.SetMapPaused(ShipyardMap.Value, false);
    }
}
