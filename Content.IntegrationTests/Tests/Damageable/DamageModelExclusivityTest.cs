using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Utility;
using Content.Shared.Backmen.Body.OrganRelations;
using Content.Shared.Backmen.Surgery.Consciousness.Components;
using Content.Shared.Backmen.Surgery.Wounds.Components;
using Content.Shared.Damage.Components;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.Damageable;

[TestFixture]
public sealed class DamageModelExclusivityTest : GameTest
{
    private static readonly string[] BackmenDamageModelPrototypes =
        GameDataScrounger.EntitiesWithComponent("Woundable")
            .Concat(GameDataScrounger.EntitiesWithComponent("Consciousness"))
            .Concat(GameDataScrounger.EntitiesWithComponent("BkmDetachedBody"))
            .Distinct()
            .ToArray();

    // start-backmen: injurable-exclusivity
    [Test]
    [TestCaseSource(nameof(BackmenDamageModelPrototypes))]
    public async Task TestSpawnedBackmenModelsHaveNoInjurableAfterMapInit(string prototype)
    {
        var map = await Pair.CreateTestMap();
        var entity = await SpawnAtPosition(prototype, map.GridCoords);

        await Server.WaitPost(() =>
        {
            Assert.That(SEntMan.HasComponent<InjurableComponent>(entity), Is.False,
                $"{prototype} must not keep Injurable after MapInit when using a backmen damage model.");
            SDeleteNow(entity);
        });
    }
    // end-backmen: injurable-exclusivity
}
