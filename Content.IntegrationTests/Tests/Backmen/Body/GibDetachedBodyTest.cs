using Content.IntegrationTests.Fixtures;
using Content.Server.Backmen.Body.Systems;
using Content.Shared.Backmen.Body.OrganRelations;
using Content.Shared.Backmen.Body.Systems;
using Content.Shared.Body;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using System.Collections.Generic;
using System.Numerics;

namespace Content.IntegrationTests.Tests.Backmen.Body;

/// <summary>
/// Full-body gib must scatter external parts into separate <see cref="BkmDetachedBodyComponent"/> bundles.
/// </summary>
[TestFixture]
public sealed class GibDetachedBodyTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [Test]
    public async Task GibBody_CreatesMultipleDetachedBundles()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var patient = Server.EntMan.SpawnEntity("MobHuman", map.MapCoords);
            var bodySys = Server.EntMan.System<BkmBodySystem>();
            bodySys.GibBody(patient, gibOrgans: true);
        });

        await Server.WaitRunTicks(90);

        await Server.WaitAssertion(() =>
        {
            var bundleCount = 0;
            var positions = new List<Vector2>();
            var enumerator = Server.EntMan.EntityQueryEnumerator<BkmDetachedBodyComponent>();
            while (enumerator.MoveNext(out var bundle, out var detached))
            {
                bundleCount++;
                Assert.That(detached.MessyScatter, Is.True, "Gib bundles should use violent scatter.");
                Assert.That(Server.EntMan.TryGetComponent(bundle, out BodyComponent? body) && body!.Organs?.Count > 0,
                    Is.True,
                    "Each detached bundle should still contain at least one organ.");
                positions.Add(Server.EntMan.GetComponent<TransformComponent>(bundle).WorldPosition);
            }

            Assert.That(bundleCount, Is.GreaterThan(3),
                "Human gib should produce a detached bundle per external part, not a single pile.");

            Assert.That(MedianPairwiseDistance(positions), Is.GreaterThanOrEqualTo(1f),
                "Gib should scatter bundles at least one tile apart on median.");
        });
    }

    private static float MedianPairwiseDistance(List<Vector2> positions)
    {
        var distances = new List<float>();
        for (var i = 0; i < positions.Count; i++)
        {
            for (var j = i + 1; j < positions.Count; j++)
                distances.Add((positions[i] - positions[j]).Length());
        }

        if (distances.Count == 0)
            return 0f;

        distances.Sort();
        return distances[distances.Count / 2];
    }
}
