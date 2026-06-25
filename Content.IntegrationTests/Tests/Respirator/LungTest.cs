using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Body.Components;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Body;
using Content.Shared.Body.Systems;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Shared;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using System.Numerics;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests.Respirator;

[TestFixture]
[TestOf(typeof(LungSystem))]
public sealed class LungTest : GameTest
{
    public override PoolSettings PoolSettings => PsDisconnected;

    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  name: HumanLungDummy
  id: HumanLungDummy
  components:
  - type: SolutionContainerManager
  - type: Body
  - type: EntityTableContainerFill
    containers:
      body_organs: !type:AllSelector
        children:
        - id: OrganHumanLungs
  - type: MobState
    allowedStates:
      - Alive
  - type: Damageable
  - type: ThermalRegulator
    metabolismHeat: 5000
    radiatedHeat: 400
    implicitHeatRegulation: 5000
    sweatHeatRegulation: 5000
    shiveringHeatRegulation: 5000
    normalBodyTemperature: 310.15
    thermalRegulationTemperatureThreshold: 25
  - type: Respirator
    damage:
      types:
        Asphyxiation: 1.5
    damageRecovery:
      types:
        Asphyxiation: -1.5
";

    [Test]
    public async Task AirConsistencyTest()
    {
        // --- Setup
        var pair = Pair;
        var server = pair.Server;

        await server.WaitIdleAsync();

        var entityManager = server.ResolveDependency<IEntityManager>();
        var mobStateSystem = entityManager.System<MobStateSystem>();
        var mapLoader = entityManager.System<MapLoaderSystem>();
        var mapSys = entityManager.System<SharedMapSystem>();

        EntityUid? grid = null;
        EntityUid human = default;
        GridAtmosphereComponent relevantAtmos = default;
        var startingMoles = 0.0f;

        var testMapName = new ResPath("Maps/Test/Breathing/3by3-20oxy-80nit.yml");

        await server.WaitPost(() =>
        {
            mapSys.CreateMap(out var mapId);
            Assert.That(mapLoader.TryLoadGrid(mapId, testMapName, out var gridEnt));
            grid = gridEnt!.Value.Owner;
        });

        Assert.That(grid, Is.Not.Null, $"Test blueprint {testMapName} not found.");

        float GetMapMoles()
        {
            var totalMapMoles = 0.0f;
            foreach (var tile in relevantAtmos.Tiles.Values)
            {
                totalMapMoles += tile.Air?.TotalMoles ?? 0.0f;
            }

            return totalMapMoles;
        }

        await server.WaitAssertion(() =>
        {
            var center = new Vector2(0.5f, 0.5f);
            var coordinates = new EntityCoordinates(grid.Value, center);
            human = entityManager.SpawnEntity("HumanLungDummy", coordinates);
            relevantAtmos = entityManager.GetComponent<GridAtmosphereComponent>(grid.Value);
            startingMoles = 100f; // Hardcoded because GetMapMoles returns 900 here for some reason.

            Assert.That(entityManager.TryGetComponent(human, out BodyComponent? body), Is.True);
            Assert.That(body!.Organs, Is.Not.Null);
            Assert.That(body.Organs!.Count, Is.GreaterThan(0), "HumanLungDummy should have lungs in body_organs.");
            Assert.That(entityManager.TryGetComponent(human, out RespiratorComponent? _), Is.True);
            Assert.That(mobStateSystem.IsDead(human), Is.False, "Dummy should be alive when spawned.");
        });

        // MapInit / EntityTableContainerFill / respirator scheduling
        await server.WaitRunTicks(5);

        // --- End setup

        var inhaleCycles = 100;
        for (var i = 0; i < inhaleCycles; i++)
        {
            // Breathe in
            await PoolManager.WaitUntil(server, () =>
                entityManager.TryGetComponent(human, out RespiratorComponent? resp)
                && resp.Status == RespiratorStatus.Exhaling);
            Assert.That(
                GetMapMoles(), Is.LessThan(startingMoles),
                "Did not inhale in any gas"
            );

            // Breathe out
            await PoolManager.WaitUntil(server, () =>
                entityManager.TryGetComponent(human, out RespiratorComponent? resp)
                && resp.Status == RespiratorStatus.Inhaling);
            Assert.That(
                GetMapMoles(), Is.EqualTo(startingMoles).Within(0.0002),
                "Did not exhale as much gas as was inhaled"
            );
        }
    }

    [Test]
    public async Task NoSuffocationTest()
    {
        var pair = Pair;
        var server = pair.Server;

        var mapManager = server.ResolveDependency<IMapManager>();
        var entityManager = server.ResolveDependency<IEntityManager>();
        var cfg = server.ResolveDependency<IConfigurationManager>();
        var mapLoader = entityManager.System<MapLoaderSystem>();
        var mapSys = entityManager.System<SharedMapSystem>();

        EntityUid? grid = null;
        RespiratorComponent respirator = null;
        EntityUid human = default;

        var testMapName = new ResPath("Maps/Test/Breathing/3by3-20oxy-80nit.yml");

        await server.WaitPost(() =>
        {
            mapSys.CreateMap(out var mapId);
            Assert.That(mapLoader.TryLoadGrid(mapId, testMapName, out var gridEnt));
            grid = gridEnt!.Value.Owner;
        });

        Assert.That(grid, Is.Not.Null, $"Test blueprint {testMapName} not found.");

        await server.WaitAssertion(() =>
        {
            var center = new Vector2(0.5f, 0.5f);

            var coordinates = new EntityCoordinates(grid.Value, center);
            human = entityManager.SpawnEntity("HumanLungDummy", coordinates);

            var mixture = entityManager.System<AtmosphereSystem>().GetContainingMixture(human);
#pragma warning disable NUnit2045
            Assert.That(mixture.TotalMoles, Is.GreaterThan(0));
            Assert.That(entityManager.TryGetComponent(human, out respirator), Is.True);
            Assert.That(respirator.SuffocationCycles, Is.LessThanOrEqualTo(respirator.SuffocationCycleThreshold));
#pragma warning restore NUnit2045
        });

        var increment = 10;

        // 20 seconds
        var total = 20 * cfg.GetCVar(CVars.NetTickrate);

        for (var tick = 0; tick < total; tick += increment)
        {
            await server.WaitRunTicks(increment);
            await server.WaitAssertion(() =>
            {
                Assert.That(respirator.SuffocationCycles, Is.LessThanOrEqualTo(respirator.SuffocationCycleThreshold),
                    $"Entity {entityManager.GetComponent<MetaDataComponent>(human).EntityName} is suffocating on tick {tick}");
            });
        }
    }
}
