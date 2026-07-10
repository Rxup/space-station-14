using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Pair;
using Content.Server.Backmen.VentCrawler;
using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Server.NodeContainer.EntitySystems;
using Content.Server.NodeContainer.Nodes;
using Content.Shared.Backmen.VentCrawler;
using Content.Shared.Atmos;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Interaction;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.NodeContainer;
using Content.Shared.Temperature.Components;
using Content.Shared.Tools.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests.Backmen.VentCrawler;

[TestFixture]
[TestOf(typeof(VentCrawlerSystem))]
public sealed class VentCrawlerTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: VentCrawlerTestMob
  components:
  - type: VentCrawler
  - type: Transform
  - type: Physics
    bodyType: KinematicController
  - type: MovementSpeedModifier
    baseWalkSpeed: 2.5
    baseSprintSpeed: 4.5
  - type: Fixtures
    fixtures:
      fix1:
        shape:
          !type:PhysShapeCircle
          radius: 0.2
";

    [Test]
    public async Task EnterVent_AddsVentCrawlingComponent()
    {
        var pair = Pair;
        var server = Server;
        var (vent, _, ventCoords) = await SetupVentLine(pair);

        await server.WaitAssertion(() =>
        {
            var ventCrawler = server.EntMan.System<VentCrawlerSystem>();
            var headcrab = server.EntMan.SpawnEntity("VentCrawlerTestMob", ventCoords);
            var crawler = server.EntMan.GetComponent<VentCrawlerComponent>(headcrab);

            Assert.That(ventCrawler.CanEnterVent(headcrab, vent, crawler, out var reason), Is.True, reason);
            Assert.That(ventCrawler.TryEnterVent(headcrab, vent), Is.True);
            Assert.That(server.EntMan.HasComponent<VentCrawlingComponent>(headcrab), Is.True);
        });
    }

    [Test]
    public async Task StepAlongPipe_MovesToNextTile()
    {
        var pair = Pair;
        var server = Server;
        var (vent, pipe, ventCoords) = await SetupVentLine(pair);

        EntityUid headcrab = default;

        await server.WaitAssertion(() =>
        {
            var ventCrawler = server.EntMan.System<VentCrawlerSystem>();
            headcrab = server.EntMan.SpawnEntity("VentCrawlerTestMob", ventCoords);
            Assert.That(ventCrawler.TryEnterVent(headcrab, vent), Is.True);
        });

        await server.WaitAssertion(() =>
        {
            var ventCrawler = server.EntMan.System<VentCrawlerSystem>();
            Assert.That(ventCrawler.TryStep(headcrab, Direction.South), Is.True);
        });

        await pair.RunTicksSync(30);

        await server.WaitAssertion(() =>
        {
            var crawling = server.EntMan.GetComponent<VentCrawlingComponent>(headcrab);
            Assert.That(crawling.CurrentPipe, Is.EqualTo(pipe));

            var pipeCoords = server.EntMan.GetComponent<TransformComponent>(pipe).Coordinates;
            var headcrabCoords = server.EntMan.GetComponent<TransformComponent>(headcrab).Coordinates;
            Assert.That(headcrabCoords, Is.EqualTo(pipeCoords));
        });
    }

    [Test]
    public async Task StepThroughPressurePump_MovesToNextTile()
    {
        var pair = Pair;
        var server = Server;
        var (vent, pump, pipe, ventCoords) = await SetupVentLineWithPump(pair);

        EntityUid headcrab = default;

        await server.WaitAssertion(() =>
        {
            var ventCrawler = server.EntMan.System<VentCrawlerSystem>();
            Assert.That(ventCrawler.IsValidCrawlPipe(pump), Is.True);

            headcrab = server.EntMan.SpawnEntity("VentCrawlerTestMob", ventCoords);
            Assert.That(ventCrawler.TryEnterVent(headcrab, vent), Is.True);
            Assert.That(ventCrawler.TryStep(headcrab, Direction.South), Is.True);
        });

        await pair.RunTicksSync(30);

        await server.WaitAssertion(() =>
        {
            var crawling = server.EntMan.GetComponent<VentCrawlingComponent>(headcrab);
            Assert.That(crawling.CurrentPipe, Is.EqualTo(pump));
        });

        await server.WaitAssertion(() =>
        {
            var ventCrawler = server.EntMan.System<VentCrawlerSystem>();
            Assert.That(ventCrawler.TryStep(headcrab, Direction.South), Is.True);
        });

        await pair.RunTicksSync(30);

        await server.WaitAssertion(() =>
        {
            var crawling = server.EntMan.GetComponent<VentCrawlingComponent>(headcrab);
            Assert.That(crawling.CurrentPipe, Is.EqualTo(pipe));
        });
    }

    [Test]
    public async Task ExitVent_RestoresMovementSpeed()
    {
        var pair = Pair;
        var server = Server;
        var (vent, _, ventCoords) = await SetupVentLine(pair);

        EntityUid headcrab = default;

        await server.WaitAssertion(() =>
        {
            var ventCrawler = server.EntMan.System<VentCrawlerSystem>();
            var movementSpeed = server.EntMan.System<MovementSpeedModifierSystem>();
            headcrab = server.EntMan.SpawnEntity("VentCrawlerTestMob", ventCoords);

            Assert.That(ventCrawler.TryEnterVent(headcrab, vent), Is.True);

            movementSpeed.RefreshMovementSpeedModifiers(headcrab);
            var crawlingMove = server.EntMan.GetComponent<MovementSpeedModifierComponent>(headcrab);
            Assert.That(crawlingMove.WalkSpeedModifier, Is.EqualTo(0f));
            Assert.That(crawlingMove.SprintSpeedModifier, Is.EqualTo(0f));

            Assert.That(ventCrawler.TryExitVent(headcrab), Is.True);
        });

        await pair.RunTicksSync(1);

        await server.WaitAssertion(() =>
        {
            var restoredMove = server.EntMan.GetComponent<MovementSpeedModifierComponent>(headcrab);
            Assert.That(restoredMove.WalkSpeedModifier, Is.EqualTo(1f));
            Assert.That(restoredMove.SprintSpeedModifier, Is.EqualTo(1f));
        });
    }

    [Test]
    public async Task ExitVent_RemovesVentCrawlingComponent()
    {
        var pair = Pair;
        var server = Server;
        var (vent, _, ventCoords) = await SetupVentLine(pair);

        await server.WaitAssertion(() =>
        {
            var ventCrawler = server.EntMan.System<VentCrawlerSystem>();
            var headcrab = server.EntMan.SpawnEntity("VentCrawlerTestMob", ventCoords);

            Assert.That(ventCrawler.TryEnterVent(headcrab, vent), Is.True);
            Assert.That(ventCrawler.TryExitVent(headcrab), Is.True);
            Assert.That(server.EntMan.HasComponent<VentCrawlingComponent>(headcrab), Is.False);
        });
    }

    [Test]
    public async Task WeldedVent_BlocksEnter()
    {
        var pair = Pair;
        var server = Server;
        var (vent, _, ventCoords) = await SetupVentLine(pair);

        await server.WaitAssertion(() =>
        {
            var ventCrawler = server.EntMan.System<VentCrawlerSystem>();
            var weldable = server.EntMan.EnsureComponent<WeldableComponent>(vent);
            weldable.IsWelded = true;

            var headcrab = server.EntMan.SpawnEntity("VentCrawlerTestMob", ventCoords);

            Assert.That(ventCrawler.TryEnterVent(headcrab, vent), Is.False);
            Assert.That(server.EntMan.HasComponent<VentCrawlingComponent>(headcrab), Is.False);
        });
    }

    [Test]
    public async Task DeadEndStep_ForcesExit()
    {
        var pair = Pair;
        var server = Server;
        var (vent, _, ventCoords) = await SetupVentLine(pair);

        EntityUid headcrab = default;

        await server.WaitAssertion(() =>
        {
            var ventCrawler = server.EntMan.System<VentCrawlerSystem>();
            headcrab = server.EntMan.SpawnEntity("VentCrawlerTestMob", ventCoords);

            Assert.That(ventCrawler.TryEnterVent(headcrab, vent), Is.True);
            Assert.That(ventCrawler.TryStep(headcrab, Direction.South), Is.True);
        });

        await pair.RunTicksSync(30);

        await server.WaitAssertion(() =>
        {
            var ventCrawler = server.EntMan.System<VentCrawlerSystem>();
            Assert.That(ventCrawler.TryStep(headcrab, Direction.South), Is.True);
            Assert.That(server.EntMan.HasComponent<VentCrawlingComponent>(headcrab), Is.False);
        });
    }

    [Test]
    public async Task BrokenPipeUnderCrawler_ForcesExit()
    {
        var pair = Pair;
        var server = Server;
        var (vent, pipe, ventCoords) = await SetupVentLine(pair);

        EntityUid headcrab = default;

        await server.WaitAssertion(() =>
        {
            var ventCrawler = server.EntMan.System<VentCrawlerSystem>();
            headcrab = server.EntMan.SpawnEntity("VentCrawlerTestMob", ventCoords);
            Assert.That(ventCrawler.TryEnterVent(headcrab, vent), Is.True);
            Assert.That(ventCrawler.TryStep(headcrab, Direction.South), Is.True);
        });

        await pair.RunTicksSync(30);

        await server.WaitPost(() =>
        {
            var damage = new DamageSpecifier();
            damage.DamageDict.Add("Blunt", 200);
            server.EntMan.System<DamageableSystem>().TryChangeDamage(pipe, damage);
        });

        await pair.RunTicksSync(15);

        await server.WaitAssertion(() =>
        {
            Assert.That(server.EntMan.HasComponent<VentCrawlingComponent>(headcrab), Is.False);
        });
    }

    [Test]
    public async Task PerpendicularStep_BlockedOnStraightPipe()
    {
        var pair = Pair;
        var server = Server;
        var (vent, _, ventCoords) = await SetupVentLine(pair);

        EntityUid headcrab = default;

        await server.WaitAssertion(() =>
        {
            var ventCrawler = server.EntMan.System<VentCrawlerSystem>();
            headcrab = server.EntMan.SpawnEntity("VentCrawlerTestMob", ventCoords);

            Assert.That(ventCrawler.TryEnterVent(headcrab, vent), Is.True);
            Assert.That(ventCrawler.TryStep(headcrab, Direction.South), Is.True);
        });

        await pair.RunTicksSync(30);

        await server.WaitAssertion(() =>
        {
            var ventCrawler = server.EntMan.System<VentCrawlerSystem>();
            var coordsBefore = server.EntMan.GetComponent<TransformComponent>(headcrab).Coordinates;

            Assert.That(ventCrawler.TryStep(headcrab, Direction.East), Is.False);
            Assert.That(ventCrawler.TryStep(headcrab, Direction.West), Is.False);
            Assert.That(server.EntMan.HasComponent<VentCrawlingComponent>(headcrab), Is.True);
            Assert.That(server.EntMan.GetComponent<TransformComponent>(headcrab).Coordinates, Is.EqualTo(coordsBefore));
        });
    }

    [Test]
    public async Task VentCrawling_BlocksInteractionWithFloorItems()
    {
        var pair = Pair;
        var server = Server;
        var (vent, pipe, ventCoords) = await SetupVentLine(pair);

        EntityUid headcrab = default;
        EntityUid floorItem = default;

        await server.WaitAssertion(() =>
        {
            var ventCrawler = server.EntMan.System<VentCrawlerSystem>();
            var interaction = server.EntMan.System<SharedInteractionSystem>();
            var entMan = server.EntMan;

            headcrab = entMan.SpawnEntity("VentCrawlerTestMob", ventCoords);
            Assert.That(ventCrawler.TryEnterVent(headcrab, vent), Is.True);

            floorItem = entMan.SpawnEntity("Crowbar", ventCoords);

            Assert.That(interaction.IsAccessible(headcrab, floorItem), Is.False);
            Assert.That(interaction.InRangeAndAccessible(headcrab, floorItem), Is.False);
            Assert.That(interaction.IsAccessible(headcrab, vent), Is.True);
            Assert.That(interaction.IsAccessible(headcrab, pipe), Is.True);
        });
    }

    [Test]
    public async Task VentCrawling_ExposesEntityToPipeAtmosphere()
    {
        var pair = Pair;
        var server = Server;
        var (vent, _, ventCoords) = await SetupVentLine(pair);

        const float pipeTemperature = 500f;
        EntityUid headcrab = default;

        await server.WaitAssertion(() =>
        {
            var ventCrawler = server.EntMan.System<VentCrawlerSystem>();
            var entMan = server.EntMan;

            headcrab = entMan.SpawnEntity("VentCrawlerTestMob", ventCoords);
            Assert.That(ventCrawler.TryEnterVent(headcrab, vent), Is.True);

            var crawling = entMan.GetComponent<VentCrawlingComponent>(headcrab);
            var nodeContainer = entMan.GetComponent<NodeContainerComponent>(crawling.CurrentPipe);
            var nodeContainerSys = entMan.System<NodeContainerSystem>();
            Assert.That(nodeContainerSys.TryGetNode(nodeContainer, "pipe", out PipeNode? pipeNode), Is.True);

            pipeNode!.Air.Temperature = pipeTemperature;

            var atmosphere = entMan.System<AtmosphereSystem>();
            var xform = entMan.GetComponent<TransformComponent>(headcrab);
            var mixture = atmosphere.GetContainingMixture((headcrab, xform));

            Assert.That(mixture, Is.Not.Null);
            Assert.That(mixture!.Temperature, Is.EqualTo(pipeTemperature).Within(0.1f));
        });
    }

    [Test]
    public async Task VentCrawling_PipeTemperatureAffectsEntity()
    {
        var pair = Pair;
        var server = Server;
        var (vent, _, ventCoords) = await SetupVentLine(pair);

        const float pipeTemperature = 500f;
        EntityUid headcrab = default;

        await server.WaitAssertion(() =>
        {
            var ventCrawler = server.EntMan.System<VentCrawlerSystem>();
            var entMan = server.EntMan;

            headcrab = entMan.SpawnEntity("VentCrawlerTestMob", ventCoords);
            entMan.EnsureComponent<AtmosExposedComponent>(headcrab);
            var temperature = entMan.EnsureComponent<TemperatureComponent>(headcrab);
            temperature.CurrentTemperature = Atmospherics.T20C;

            Assert.That(ventCrawler.TryEnterVent(headcrab, vent), Is.True);

            var crawling = entMan.GetComponent<VentCrawlingComponent>(headcrab);
            var nodeContainer = entMan.GetComponent<NodeContainerComponent>(crawling.CurrentPipe);
            var nodeContainerSys = entMan.System<NodeContainerSystem>();
            Assert.That(nodeContainerSys.TryGetNode(nodeContainer, "pipe", out PipeNode? pipeNode), Is.True);
            pipeNode!.Air.Temperature = pipeTemperature;
            pipeNode.Air.AdjustMoles(Gas.Oxygen, 50f);
        });

        await pair.RunTicksSync(90);

        await server.WaitAssertion(() =>
        {
            var temperature = server.EntMan.GetComponent<TemperatureComponent>(headcrab);
            Assert.That(temperature.CurrentTemperature, Is.GreaterThan(Atmospherics.T20C + 1f));
        });
    }

    [Test]
    public async Task StepIntoBrokenPipe_ForcesExitAtGap()
    {
        var pair = Pair;
        var server = Server;
        var (vent, _, ventCoords) = await SetupVentLine(pair, extraPipeTile: new Vector2i(0, -1), broken: true);

        EntityUid headcrab = default;

        await server.WaitAssertion(() =>
        {
            var ventCrawler = server.EntMan.System<VentCrawlerSystem>();
            headcrab = server.EntMan.SpawnEntity("VentCrawlerTestMob", ventCoords);
            Assert.That(ventCrawler.TryEnterVent(headcrab, vent), Is.True);
            Assert.That(ventCrawler.TryStep(headcrab, Direction.South), Is.True);
        });

        await pair.RunTicksSync(30);

        await server.WaitAssertion(() =>
        {
            var ventCrawler = server.EntMan.System<VentCrawlerSystem>();
            Assert.That(ventCrawler.TryStep(headcrab, Direction.South), Is.True);
            Assert.That(server.EntMan.HasComponent<VentCrawlingComponent>(headcrab), Is.False);

            var entMan = server.EntMan;
            var mapSys = entMan.System<SharedMapSystem>();
            var headcrabXform = entMan.GetComponent<TransformComponent>(headcrab);
            var gridUid = headcrabXform.GridUid!.Value;
            var grid = entMan.GetComponent<MapGridComponent>(gridUid);
            var tile = mapSys.TileIndicesFor(gridUid, grid, headcrabXform.Coordinates);
            Assert.That(tile, Is.EqualTo(new Vector2i(0, -1)));
        });
    }

    [Test]
    public async Task UnanchoredPipeUnderCrawler_ForcesExit()
    {
        var pair = Pair;
        var server = Server;
        var (vent, pipe, ventCoords) = await SetupVentLine(pair);

        EntityUid headcrab = default;

        await server.WaitAssertion(() =>
        {
            var ventCrawler = server.EntMan.System<VentCrawlerSystem>();
            headcrab = server.EntMan.SpawnEntity("VentCrawlerTestMob", ventCoords);
            Assert.That(ventCrawler.TryEnterVent(headcrab, vent), Is.True);
            Assert.That(ventCrawler.TryStep(headcrab, Direction.South), Is.True);
        });

        await pair.RunTicksSync(30);

        await server.WaitAssertion(() =>
        {
            var xformSystem = server.EntMan.System<SharedTransformSystem>();
            xformSystem.Unanchor(pipe, server.EntMan.GetComponent<TransformComponent>(pipe));
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(server.EntMan.HasComponent<VentCrawlingComponent>(headcrab), Is.False);
        });
    }

    private static async Task<(EntityUid Vent, EntityUid Pipe, EntityCoordinates VentCoords)> SetupVentLine(
        TestPair pair,
        Vector2i? extraPipeTile = null,
        bool broken = false)
    {
        var testMap = await pair.CreateTestMap();
        EntityUid vent = default;
        EntityUid pipe = default;
        EntityCoordinates ventCoords = default;

        await pair.Server.WaitAssertion(() =>
        {
            var entMan = pair.Server.EntMan;
            var mapSys = entMan.System<SharedMapSystem>();
            var gridUid = testMap.Grid.Owner;
            var grid = testMap.Grid.Comp;

            var ventTile = new Vector2i(0, 1);
            var pipeTile = Vector2i.Zero;

            mapSys.SetTile(gridUid, grid, pipeTile, new Tile(1));
            mapSys.SetTile(gridUid, grid, ventTile, new Tile(1));

            vent = entMan.SpawnEntity("GasVentPump", new EntityCoordinates(gridUid, ventTile));
            pipe = entMan.SpawnEntity("GasPipeStraight", new EntityCoordinates(gridUid, pipeTile));

            if (extraPipeTile is { } extraTile)
            {
                mapSys.SetTile(gridUid, grid, extraTile, new Tile(1));
                entMan.SpawnEntity(broken ? "GasPipeBroken" : "GasPipeStraight", new EntityCoordinates(gridUid, extraTile));
            }

            ventCoords = entMan.GetComponent<TransformComponent>(vent).Coordinates;
        });

        await pair.RunTicksSync(15);
        return (vent, pipe, ventCoords);
    }

    private static async Task<(EntityUid Vent, EntityUid Pump, EntityUid Pipe, EntityCoordinates VentCoords)> SetupVentLineWithPump(
        TestPair pair)
    {
        var testMap = await pair.CreateTestMap();
        EntityUid vent = default;
        EntityUid pump = default;
        EntityUid pipe = default;
        EntityCoordinates ventCoords = default;

        await pair.Server.WaitAssertion(() =>
        {
            var entMan = pair.Server.EntMan;
            var mapSys = entMan.System<SharedMapSystem>();
            var gridUid = testMap.Grid.Owner;
            var grid = testMap.Grid.Comp;

            var ventTile = new Vector2i(0, 2);
            var pumpTile = new Vector2i(0, 1);
            var pipeTile = Vector2i.Zero;

            mapSys.SetTile(gridUid, grid, pipeTile, new Tile(1));
            mapSys.SetTile(gridUid, grid, pumpTile, new Tile(1));
            mapSys.SetTile(gridUid, grid, ventTile, new Tile(1));

            vent = entMan.SpawnEntity("GasVentPump", new EntityCoordinates(gridUid, ventTile));
            pump = entMan.SpawnEntity("GasPressurePump", new EntityCoordinates(gridUid, pumpTile));
            pipe = entMan.SpawnEntity("GasPipeStraight", new EntityCoordinates(gridUid, pipeTile));

            ventCoords = entMan.GetComponent<TransformComponent>(vent).Coordinates;
        });

        await pair.RunTicksSync(15);
        return (vent, pump, pipe, ventCoords);
    }
}
