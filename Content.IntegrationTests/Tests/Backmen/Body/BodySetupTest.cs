using System.Linq;
using Content.Server.Administration.Systems;
using Content.Server.Body.Systems;
using Content.Server.Hands.Systems;
using Content.Shared.Backmen.Surgery.Consciousness.Components;
using Content.Shared.Backmen.Surgery.Consciousness.Systems;
using Content.Shared.Backmen.Surgery.Wounds.Components;
using Content.Shared.Backmen.Surgery.Wounds.Systems;
using Content.Shared.Body.Components;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Part;
using Content.Shared.Humanoid.Prototypes;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.Backmen.Body;

[TestFixture]
public sealed class BodySetupTest
{
    [Test]
    public async Task AllSpeciesHaveLegs()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Dirty = true,
            Connected = true,
            InLobby = false,
        });

        var server = pair.Server;
        var bodySys = server.EntMan.System<BodySystem>();

        foreach (var speciesPrototype in server.ProtoMan.EnumeratePrototypes<SpeciesPrototype>())
        {
            var dummy = EntityUid.Invalid;
            await server.WaitAssertion(() =>
            {
                dummy = server.EntMan.Spawn(speciesPrototype.Prototype);
            });
            await server.WaitIdleAsync();
            await server.WaitRunTicks(2);
            await server.WaitAssertion(() =>
            {
                Assert.That(dummy, Is.Not.EqualTo(EntityUid.Invalid));
                var bodyComp = server.EntMan.GetComponent<BodyComponent>(dummy);
                var legs = bodyComp.LegEntities;
                var legsCount = bodySys.GetBodyPartCount(dummy, BodyPartType.Leg);
                Assert.That(legsCount, Is.EqualTo(legs.Count));
                Assert.That(legsCount, Is.GreaterThanOrEqualTo(2), $"legs {speciesPrototype.ID}({speciesPrototype.Prototype})");
            });

        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AllSpeciesHaveHands()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Dirty = true,
            Connected = true,
            InLobby = false,
        });

        var server = pair.Server;
        var handsSys = server.EntMan.System<HandsSystem>();

        foreach (var speciesPrototype in server.ProtoMan.EnumeratePrototypes<SpeciesPrototype>())
        {
            var dummy = EntityUid.Invalid;
            await server.WaitAssertion(() =>
            {
                dummy = server.EntMan.Spawn(speciesPrototype.Prototype);
            });
            await server.WaitIdleAsync();
            await server.WaitRunTicks(2);
            await server.WaitAssertion(() =>
            {
                Assert.That(dummy, Is.Not.EqualTo(EntityUid.Invalid));
                var handCount = handsSys.EnumerateHands(dummy).Count();
                Assert.That(handCount, Is.GreaterThanOrEqualTo(2), $"hands {speciesPrototype.ID}({speciesPrototype.Prototype})");
            });

        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AllSpeciesAreConscious()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Dirty = true,
            Connected = true,
            InLobby = false,
        });

        var server = pair.Server;
        var consciousnessSystem = server.EntMan.System<ConsciousnessSystem>();
        var bodySystem = server.EntMan.System<BodySystem>();

        foreach (var speciesPrototype in server.ProtoMan.EnumeratePrototypes<SpeciesPrototype>())
        {
            var dummy = EntityUid.Invalid;
            await server.WaitAssertion(() =>
            {
                dummy = server.EntMan.Spawn(speciesPrototype.Prototype);
            });
            await server.WaitIdleAsync();
            await server.WaitRunTicks(2);
            await server.WaitAssertion(() =>
            {
                Assert.That(dummy, Is.Not.EqualTo(EntityUid.Invalid));
                Assert.That(server.EntMan.TryGetComponent(dummy, out ConsciousnessComponent consciousness));

                Assert.That(consciousnessSystem.TryGetNerveSystem(dummy, out var dummyNerveSys), Is.True);

                Assert.That(server.EntMan.HasComponent<OrganComponent>(dummyNerveSys));
                Assert.That(server.EntMan.HasComponent<ConsciousnessRequiredComponent>(dummyNerveSys));

                var part = EntityUid.Invalid;
                foreach (var bodyPart in bodySystem.GetBodyChildren(dummy))
                {
                    foreach (var organ in bodySystem.GetPartOrgans(bodyPart.Id, bodyPart.Component))
                    {
                        if (organ.Id == dummyNerveSys)
                            part = bodyPart.Id;
                    }
                }

                Assert.That(part, Is.Not.EqualTo(EntityUid.Invalid));
                Assert.That(server.EntMan.HasComponent<ConsciousnessRequiredComponent>(part));

                Assert.That(consciousness.Consciousness, Is.GreaterThan(consciousness.Threshold));
            });

        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AllSpeciesCanBeRejuvenated()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Dirty = true,
            Connected = true,
            InLobby = false,
        });

        var server = pair.Server;
        var bodySystem = server.EntMan.System<BodySystem>();
        var woundSystem = server.EntMan.System<WoundSystem>();
        var rejuvenateSystem = server.EntMan.System<RejuvenateSystem>();

        foreach (var speciesPrototype in server.ProtoMan.EnumeratePrototypes<SpeciesPrototype>())
        {
            var dummy = EntityUid.Invalid;
            await server.WaitAssertion(() =>
            {
                dummy = server.EntMan.Spawn(speciesPrototype.Prototype);
            });
            await server.WaitIdleAsync();
            await server.WaitRunTicks(2);
            await server.WaitAssertion(() =>
            {
                var initialBodyPartCount = bodySystem.GetBodyPartCount(dummy, BodyPartType.Head);
                var headEntity = bodySystem.GetBodyChildrenOfType(dummy, BodyPartType.Head).FirstOrDefault();
                var groinEntity = bodySystem.GetBodyChildrenOfType(dummy, BodyPartType.Groin).FirstOrDefault();

                Assert.That(bodySystem.TryGetParentBodyPart(headEntity.Id, out var parentPart, out _));
                Assert.That(parentPart, Is.Not.Null);

                Assert.That(server.EntMan.TryGetComponent(parentPart, out WoundableComponent woundable));

                // Destroy the head, and damage the groin so we can check.
                woundSystem.DestroyWoundable(parentPart.Value, headEntity.Id, woundable);
                woundSystem.TryCreateWound(groinEntity.Id, "Blunt", 25f, "Brute");

                rejuvenateSystem.PerformRejuvenate(dummy);

                Assert.That(initialBodyPartCount, Is.EqualTo(bodySystem.GetBodyPartCount(dummy, BodyPartType.Head)));

                Assert.That(woundSystem.GetWoundableSeverityPoint(parentPart.Value), Is.Zero);
                Assert.That(woundSystem.GetWoundableSeverityPoint(groinEntity.Id), Is.Zero);
            });

        }

        await pair.CleanReturnAsync();
    }
}
