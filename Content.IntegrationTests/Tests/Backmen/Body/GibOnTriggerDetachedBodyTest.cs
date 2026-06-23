using Content.IntegrationTests.Fixtures;
using Content.Shared.Backmen.Body.OrganRelations;
using Content.Shared.Body;
using Content.Shared.Trigger;
using Content.Shared.Trigger.Components.Effects;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.Backmen.Body;

/// <summary>
/// Gib-on-trigger effects (e.g. death acidifier) must use backmen body gibbing, not legacy organ giblets.
/// </summary>
[TestFixture]
public sealed class GibOnTriggerDetachedBodyTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [Test]
    public async Task GibOnTrigger_CreatesDetachedBundles()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var patient = Server.EntMan.SpawnEntity("MobHuman", map.MapCoords);
            Server.EntMan.AddComponent<GibOnTriggerComponent>(patient);

            var triggerEv = new TriggerEvent(patient);
            Server.EntMan.EventBus.RaiseLocalEvent(patient, ref triggerEv);

            Assert.That(triggerEv.Handled, Is.True);
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
                "Gib-on-trigger should produce detached bundles per external part.");
        });
    }
}
