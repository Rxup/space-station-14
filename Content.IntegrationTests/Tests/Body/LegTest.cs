using Content.IntegrationTests.Fixtures;
using Content.Server.Backmen.Body.Systems;
using Content.Shared.Backmen.Targeting;
using Content.Shared.Body;
using Content.Shared.Rotation;
using Content.Shared.Standing;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Body;

/// <summary>
/// Nubody version: removing leg organs clears <see cref="BodyComponent.LegEntities"/> and prevents standing.
/// </summary>
[TestFixture]
[TestOf(typeof(BodyComponent))]
public sealed class LegTest : GameTest
{
    public override PoolSettings PoolSettings => PsDisconnected;

    [Test]
    public async Task RemoveLegsFallTest()
    {
        var pair = Pair;
        var server = pair.Server;

        var entityManager = server.ResolveDependency<IEntityManager>();
        var bodySystem = entityManager.System<BkmBodySystem>();
        var bodyOrgans = entityManager.System<BodySystem>();
        var standing = entityManager.System<StandingStateSystem>();
        var appearanceSystem = entityManager.System<SharedAppearanceSystem>();

        var map = await pair.CreateTestMap();

        EntityUid human = default;
        AppearanceComponent? appearance = null;

        await server.WaitAssertion(() =>
        {
            human = entityManager.SpawnEntity("MobHuman", map.MapCoords);
            Assert.That(entityManager.TryGetComponent(human, out BodyComponent? body));
            Assert.That(body!.Organs, Is.Not.Null);
            Assert.That(body.LegEntities, Is.Not.Empty);
            Assert.That(entityManager.TryGetComponent(human, out appearance));
            Assert.That(
                appearanceSystem.TryGetData(human, RotationVisuals.RotationState, out RotationState _, appearance),
                Is.False);
        });

        await server.WaitRunTicks(5);

        await server.WaitAssertion(() =>
        {
            var body = entityManager.GetComponent<BodyComponent>(human);

            foreach (var category in new ProtoId<OrganCategoryPrototype>[] { "LegLeft", "LegRight" })
            {
                if (!bodyOrgans.TryGetOrganByCategory((human, body), category, out var leg))
                    continue;

                Assert.That(bodySystem.RemoveOrgan(leg), Is.True, $"Failed to remove {category}.");
            }

            Assert.That(body.LegEntities, Is.Empty);

            var standAttempt = new StandAttemptEvent();
            entityManager.EventBus.RaiseLocalEvent(human, standAttempt);
            Assert.That(standAttempt.Cancelled, Is.True, "Mob without legs should not be able to stand.");
            Assert.That(standing.IsDown(human), Is.True, "Mob without legs should be knocked down.");
        });

        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
#pragma warning disable NUnit2045
            Assert.That(
                appearanceSystem.TryGetData(human, RotationVisuals.RotationState, out RotationState state, appearance),
                Is.True);
            Assert.That(state, Is.EqualTo(RotationState.Horizontal));
#pragma warning restore NUnit2045
        });
    }
}
