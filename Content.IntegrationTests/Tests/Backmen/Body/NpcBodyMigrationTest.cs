using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server.Backmen.Body.Systems;
using Content.Shared.Backmen.Body.Systems;
using Content.Shared.Body;
using Content.Shared.Body.Part;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.Backmen.Body;

[TestFixture]
public sealed class NpcBodyMigrationTest : GameTest
{
  private static readonly string[] MigratedNpcPrototypes =
  [
      "MobCarp",
      "MobHaisenberg",
      "MobGolemCult",
      "MobIPC",
      "KsenosXeno",
  ];

  public override PoolSettings PoolSettings => new()
  {
      Dirty = true,
      Connected = false,
      InLobby = false,
  };

  [Test]
  public async Task MigratedNpcsUseNubodyOrgans()
  {
    var entMan = Server.ResolveDependency<IEntityManager>();
    var bodySystem = entMan.System<BkmBodySystem>();

    var testMap = await Pair.CreateTestMap();

    foreach (var protoId in MigratedNpcPrototypes)
    {
      EntityUid mob = default;

      await Server.WaitAssertion(() =>
      {
        mob = entMan.Spawn(protoId, testMap.MapCoords);
      });

      await Server.WaitIdleAsync();
      await Server.WaitRunTicks(2);

      await Server.WaitAssertion(() =>
      {
        Assert.That(entMan.TryGetComponent(mob, out BodyComponent body), Is.True, protoId);
        Assert.That(body.Organs, Is.Not.Null, protoId);
        Assert.That(body.Organs!.ContainedEntities.Count, Is.GreaterThan(0), protoId);

        var woundables = bodySystem.GetWoundableTargets(mob).ToList();
        Assert.That(woundables, Is.Not.Empty, protoId);
        Assert.That(woundables.All(uid => entMan.HasComponent<OrganComponent>(uid)), Is.True, protoId);

        Assert.That(bodySystem.TryGetWoundableTargetByType(mob, BodyPartType.Head, null, out _), Is.True, protoId);
        Assert.That(bodySystem.TryGetWoundableTargetByType(mob, BodyPartType.Chest, null, out _), Is.True, protoId);
      });
    }
  }
}
