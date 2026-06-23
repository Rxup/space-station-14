using Content.IntegrationTests.Fixtures;
using Content.Server.Backmen.Body.Systems;
using Content.Shared.Backmen.Body.OrganRelations;
using Content.Shared.Backmen.Body.Systems;
using Content.Shared.Body;
using Robust.Shared.GameObjects;

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

        await Server.WaitIdleAsync();

        await Server.WaitAssertion(() =>
        {
            var bundleCount = 0;
            var enumerator = Server.EntMan.EntityQueryEnumerator<BkmDetachedBodyComponent>();
            while (enumerator.MoveNext(out var bundle))
            {
                bundleCount++;
                Assert.That(Server.EntMan.TryGetComponent(bundle.Owner, out BodyComponent? body) && body!.Organs?.Count > 0,
                    Is.True,
                    "Each detached bundle should still contain at least one organ.");
            }

            Assert.That(bundleCount, Is.GreaterThan(3),
                "Human gib should produce a detached bundle per external part, not a single pile.");
        });
    }
}
