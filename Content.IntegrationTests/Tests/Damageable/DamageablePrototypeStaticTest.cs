using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Utility;

namespace Content.IntegrationTests.Tests.Damageable;

/// <summary>
/// YAML-only audits for damageable prototypes. Does not inherit <see cref="Fixtures.GameTest"/> so it runs in milliseconds.
/// </summary>
[TestFixture]
public sealed class DamageablePrototypeStaticTest
{
    [Test]
    public void TestDamageableMissingInjurableStatic()
    {
        var damageable = GameDataScrounger.EntitiesWithComponent("Damageable").ToHashSet();
        var skip = new HashSet<string>(
            GameDataScrounger.EntitiesWithComponent("Injurable")
                .Concat(GameDataScrounger.EntitiesWithComponent("Consciousness"))
                .Concat(GameDataScrounger.EntitiesWithComponent("Woundable"))
                .Concat(GameDataScrounger.EntitiesWithComponent("BkmDetachedBody"))
                .Concat(GameDataScrounger.EntitiesWithComponent("Godmode")));

        var missing = damageable
            .Where(id => !skip.Contains(id))
            .OrderBy(id => id)
            .ToList();

        if (missing.Count > 0)
            Assert.Fail($"Prototypes with Damageable but no Injurable (and not skipped):\n{string.Join("\n", missing)}");
    }

    // start-backmen: injurable-exclusivity
    [Test]
    public void TestBackmenDamageModelsMustNotHaveInjurableStatic()
    {
        var backmenModels = GameDataScrounger.EntitiesWithComponent("Woundable")
            .Concat(GameDataScrounger.EntitiesWithComponent("Consciousness"))
            .Concat(GameDataScrounger.EntitiesWithComponent("BkmDetachedBody"))
            .ToHashSet();

        var withInjurable = GameDataScrounger.EntitiesWithComponent("Injurable").ToHashSet();

        var conflicts = backmenModels.Where(withInjurable.Contains).OrderBy(id => id).ToList();
        if (conflicts.Count > 0)
            Assert.Fail($"Prototypes must not combine Injurable with backmen damage models:\n{string.Join("\n", conflicts)}");
    }
    // end-backmen: injurable-exclusivity
}
