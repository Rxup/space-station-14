using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server.Backmen.Body.Systems;
using Content.Shared.Body;
using Content.Shared.Body.Organ;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests.Body;

/// <summary>
/// Nubody version: flat organs in <see cref="BodyComponent.ContainerID"/> survive map save/load with body links intact.
/// </summary>
[TestFixture]
public sealed class SaveLoadReparentTest : GameTest
{
    public override PoolSettings PoolSettings => PsDisconnected;

    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  name: HumanBodyDummy
  id: HumanBodySaveDummy
  save: true
  components:
  - type: Body
  - type: EntityTableContainerFill
    containers:
      body_organs: !type:AllSelector
        children:
        - id: OrganHumanTorso
        - id: OrganHumanHead
        - id: OrganHumanLegLeft
        - id: OrganHumanLegRight
        - id: OrganHumanLungs
";

    [Test]
    public async Task Test()
    {
        var pair = Pair;
        var server = pair.Server;

        var entities = server.ResolveDependency<IEntityManager>();
        var mapManager = server.ResolveDependency<IMapManager>();
        var mapLoader = entities.System<MapLoaderSystem>();
        var bodySystem = entities.System<BkmBodySystem>();
        var bodyOrgans = entities.System<BodySystem>();
        var mapSys = entities.System<SharedMapSystem>();

        await server.WaitPost(() =>
        {
            mapSys.CreateMap(out var mapId);
            var grid = mapManager.CreateGridEntity(mapId);
            entities.RunMapInit(grid.Owner, entities.GetComponent<MetaDataComponent>(grid));
            var human = entities.SpawnEntity("HumanBodySaveDummy", new MapCoordinates(0, 0, mapId));
            entities.RunMapInit(human, entities.GetComponent<MetaDataComponent>(human));

            AssertOrgansLinkedToBody(entities, bodySystem, bodyOrgans, human);

            var mapPath = new ResPath($"/{nameof(SaveLoadReparentTest)}{nameof(Test)}map.yml");

            Assert.That(mapLoader.TrySaveMap(mapId, mapPath));
            mapSys.DeleteMap(mapId);

            Assert.That(mapLoader.TryLoadMap(mapPath, out var map, out _), Is.True);

            var loaded = FindByPrototype(entities, "HumanBodySaveDummy").ToArray();
            Assert.That(loaded, Is.Not.Empty);

            foreach (var uid in loaded)
            {
                AssertOrgansLinkedToBody(entities, bodySystem, bodyOrgans, uid);
            }

            entities.DeleteEntity(map!.Value);
        });
    }

    private static IEnumerable<EntityUid> FindByPrototype(IEntityManager entities, string protoId)
    {
        var query = entities.EntityQueryEnumerator<BodyComponent, MetaDataComponent>();
        while (query.MoveNext(out var uid, out _, out var meta))
        {
            if (meta.EntityPrototype?.ID == protoId)
                yield return uid;
        }
    }

    private static void AssertOrgansLinkedToBody(
        IEntityManager entities,
        BkmBodySystem bodySystem,
        BodySystem bodyOrgans,
        EntityUid human)
    {
        Assert.That(entities.TryGetComponent(human, out BodyComponent? body), Is.True);
        Assert.That(body!.Organs, Is.Not.Null);

        var organs = bodySystem.GetBodyOrgans(human, body).ToArray();
        Assert.That(organs, Is.Not.Empty);

        foreach (var (organUid, organ) in organs)
        {
            Assert.Multiple(() =>
            {
                Assert.That(organ.Body, Is.EqualTo(human));
                Assert.That(entities.HasComponent<OrganComponent>(organUid), Is.True);
                Assert.That(body.Organs!.ContainedEntities.Contains(organUid), Is.True);
            });
        }

        Assert.That(bodyOrgans.TryGetOrganByCategory((human, body), "Torso", out _), Is.True);
        Assert.That(bodyOrgans.TryGetOrganByCategory((human, body), "Head", out _), Is.True);
        Assert.That(bodyOrgans.TryGetOrganByCategory((human, body), "Lungs", out _), Is.True);
    }
}
