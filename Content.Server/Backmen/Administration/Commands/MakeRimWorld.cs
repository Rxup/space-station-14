using Content.Server.Administration;
using Content.Server.Administration.Logs;
using Content.Server.Atmos;
using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Parallax;
using Content.Shared.Administration;
using Content.Shared.Atmos;
using Content.Shared.Gravity;
using Content.Shared.Parallax.Biomes;
using Robust.Shared.Console;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Backmen.Administration.Commands;

[AdminCommand(AdminFlags.Mapping)]
public sealed class MakeRimWorld : IConsoleCommand
{
    public string Command => "makerimworld";

    public string Description => "помещает отпечатки и ДНК жертвы на указанном объекте (волокна перчаток не берутся)";

    public string Help => "makerimworld";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var _prototypeManager = IoCManager.Resolve<IPrototypeManager>();
        var _entManager = IoCManager.Resolve<IEntityManager>();
        var _mapManager = IoCManager.Resolve<IMapManager>();
        var _random = IoCManager.Resolve<IRobustRandom>();
        if (!_prototypeManager.TryIndex<BiomeTemplatePrototype>("Grasslands", out var biomeTemplate))
        {
            return;
        }

        var mapId = _mapManager.NextMapId();
        _mapManager.CreateMap(mapId);

        var mapUid = _mapManager.GetMapEntityId(mapId);
        MetaDataComponent? metadata = null;

        var biome = _entManager.EnsureComponent<BiomeComponent>(mapUid);

        var biomeSystem = _entManager.System<BiomeSystem>();
        biomeSystem.SetSeed(biome, _random.Next());
        biomeSystem.SetTemplate(biome, biomeTemplate);
        biomeSystem.AddMarkerLayer(biome, "Carps");
        biomeSystem.AddMarkerLayer(biome, "OreTin");
        biomeSystem.AddMarkerLayer(biome, "OreGold");
        biomeSystem.AddMarkerLayer(biome, "OreSilver");
        biomeSystem.AddMarkerLayer(biome, "OrePlasma");
        biomeSystem.AddMarkerLayer(biome, "OreUranium");
        biomeSystem.AddTemplate(biome, "Loot", _prototypeManager.Index<BiomeTemplatePrototype>("Caves"), 1);
        _entManager.Dirty(biome);

        var gravity = _entManager.EnsureComponent<GravityComponent>(mapUid);
        gravity.Enabled = true;
        _entManager.Dirty(gravity, metadata);

        var light = _entManager.EnsureComponent<MapLightComponent>(mapUid);
        light.AmbientLightColor = Color.FromHex("#2b3143");
        _entManager.Dirty(light, metadata);

        var atmos = _entManager.EnsureComponent<MapAtmosphereComponent>(mapUid);

        var moles = new float[Atmospherics.AdjustedNumberOfGases];
        moles[(int) Gas.Oxygen] = 21.824779f;
        moles[(int) Gas.Nitrogen] = 82.10312f;

        var mixture = new GasMixture(2500)
        {
            Temperature = 293.15f,
            Moles = moles,
        };

        _entManager.System<AtmosphereSystem>().SetMapAtmosphere(mapUid, false, mixture, atmos);
        _entManager.EnsureComponent<MapGridComponent>(mapUid);
    }
}
