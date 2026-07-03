using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server._Lavaland.Procedural.Components;
using Content.Server._Lavaland.Procedural.Systems;
using Content.Server.GameTicking;
using Content.Shared._Lavaland.Procedural.Components;
using Content.Shared._Lavaland.Procedural.Prototypes;
using Content.Shared.CCVar;
using Content.Shared.Parallax.Biomes;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Lavaland;

[TestFixture]
[TestOf(typeof(LavalandPlanetSystem))]
public sealed class LavalandGenerationTest : GameTest
{
    public override PoolSettings PoolSettings => new()
    {
        DummyTicker = false,
        Dirty = true,
        Fresh = true,
    };

    [Test]
    public async Task LavalandPlanetGenerationTest()
    {
        var pair = Pair;
        var entMan = Server.EntMan;
        var protoMan = Server.ProtoMan;
        var mapSystem = entMan.System<SharedMapSystem>();

        var ticker = Server.System<GameTicker>();
        var lavaSystem = entMan.System<LavalandPlanetSystem>();

        // Setup
        Server.CfgMan.SetCVar(CCVars.LavalandEnabled, true);
        Server.CfgMan.SetCVar(CCVars.GameDummyTicker, false);
        var gameMap = Server.CfgMan.GetCVar(CCVars.GameMap);
        Server.CfgMan.SetCVar(CCVars.GameMap, "Saltern");
        var gameMode = Server.CfgMan.GetCVar(CCVars.GameLobbyDefaultPreset);
        Server.CfgMan.SetCVar(CCVars.GameLobbyDefaultPreset, "secret");

        await Server.WaitPost(() => ticker.RestartRound());
        await pair.RunTicksSync(25);
        Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.InRound));

        // Get all possible types of Lavaland and test them.
        var planets = protoMan.EnumeratePrototypes<LavalandMapPrototype>().ToList();
        foreach (var planet in planets)
        {
            const int seed = 1;

            var attempt = false;
            Entity<LavalandMapComponent>? lavaland = null;

            // Seed is always the same to reduce randomness
            await Server.WaitPost(() => lavaSystem.EnsurePreloaderMap());
            await Server.WaitPost(() => attempt = lavaSystem.SetupLavalandPlanet(out lavaland, planet, seed));
            await pair.RunTicksSync(30);

            Assert.That(attempt, Is.True);
            Assert.That(lavaland, Is.Not.Null);

            var mapId = entMan.GetComponent<TransformComponent>(lavaland.Value).MapID;

            // Now check the basics
            Assert.That(mapSystem.MapExists(mapId));
            Assert.That(entMan.EntityExists(lavaland.Value.Owner));
            Assert.That(entMan.EntityExists(lavaland.Value.Comp.Outpost));
            Assert.That(mapSystem.GetAllGrids(mapId).ToList(), Is.Not.Empty);
            Assert.That(mapSystem.IsInitialized(mapId));
            Assert.That(mapSystem.IsPaused(mapId), Is.False);

            // Test that the biome setup is right
            var biome = entMan.GetComponent<BiomeComponent>(lavaland.Value);
            Assert.That(biome.Enabled, Is.True);
            Assert.That(biome.Seed, Is.EqualTo(seed));
            Assert.That(biome.Template, Is.Not.Null);
            Assert.That(biome.Layers, Is.Not.Empty);
        }

        await pair.RunTicksSync(10);

        var lavalands = lavaSystem.GetLavalands().ToArray();
        Assert.That(planets, Has.Count.EqualTo(lavalands.Length));

        // Cleanup
        foreach (var lavaland in lavalands)
        {
            entMan.QueueDeleteEntity(lavaland);
        }

        await pair.RunTicksSync(10);

        Server.CfgMan.SetCVar(CCVars.GameMap, gameMap);
        Server.CfgMan.SetCVar(CCVars.GameLobbyDefaultPreset, gameMode);
        pair.ClearModifiedCvars();
    }
}
