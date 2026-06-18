using Content.IntegrationTests.Pair;
using Content.Server.Backmen.VentCrawler;
using Content.Shared.Backmen.VentCrawler;
using Content.Shared.Coordinates;
using Content.Shared.Tools.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests.Backmen.VentCrawler;

[TestFixture]
[TestOf(typeof(VentCrawlerSystem))]
public sealed class VentCrawlerTest
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
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
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
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
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
    public async Task ExitVent_RemovesVentCrawlingComponent()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
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
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
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
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
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
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
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
            server.EntMan.DeleteEntity(pipe);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(server.EntMan.HasComponent<VentCrawlingComponent>(headcrab), Is.False);
        });
    }

    [Test]
    public async Task StepIntoBrokenPipe_ForcesExitAtGap()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
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

    private static async Task<(EntityUid Vent, EntityUid Pipe, EntityCoordinates VentCoords)> SetupVentLine(
        TestPair pair,
        Vector2i? extraPipeTile = null,
        bool broken = false)
    {
        EntityUid vent = default;
        EntityUid pipe = default;
        EntityCoordinates ventCoords = default;

        await pair.Server.WaitAssertion(() =>
        {
            var entMan = pair.Server.EntMan;
            var mapSys = entMan.System<SharedMapSystem>();
            var mapManager = pair.Server.ResolveDependency<IMapManager>();

            var grid = mapManager.CreateGridEntity(mapSys.CreateMap(out var mapId));
            var gridUid = grid.Owner;

            var ventTile = new Vector2i(0, 1);
            var pipeTile = Vector2i.Zero;

            mapSys.SetTile(gridUid, grid.Comp, pipeTile, new Tile(1));
            mapSys.SetTile(gridUid, grid.Comp, ventTile, new Tile(1));

            vent = entMan.SpawnEntity("GasVentPump", gridUid.ToCoordinates(ventTile));

            pipe = entMan.SpawnEntity("GasPipeStraight", gridUid.ToCoordinates(pipeTile));

            if (extraPipeTile is { } extraTile)
            {
                mapSys.SetTile(gridUid, grid.Comp, extraTile, new Tile(1));
                entMan.SpawnEntity(broken ? "GasPipeBroken" : "GasPipeStraight", gridUid.ToCoordinates(extraTile));
            }

            ventCoords = entMan.GetComponent<TransformComponent>(vent).Coordinates;
        });

        await pair.RunTicksSync(15);
        return (vent, pipe, ventCoords);
    }
}
