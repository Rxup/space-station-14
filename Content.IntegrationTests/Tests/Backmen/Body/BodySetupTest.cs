﻿using System.Collections.Generic;
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
using Content.Shared.FixedPoint;
using Content.Shared.Humanoid.Prototypes;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.Backmen.Body;

[TestFixture]
public sealed class BodySetupTest
{
    /// <summary>
    /// A list of species that can be ignored by this test.
    /// </summary>
    private readonly HashSet<string> _ignoredPrototypes = new()
    {
        "Skeleton",
        "Monkey",
    };

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

        var entMan = server.ResolveDependency<IEntityManager>();
        var consciousnessSystem = entMan.System<ConsciousnessSystem>();

        await server.WaitAssertion(() =>
        {
            foreach (var speciesPrototype in server.ProtoMan.EnumeratePrototypes<SpeciesPrototype>())
            {
                if (_ignoredPrototypes.Contains(speciesPrototype.ID))
                    continue;

                var dummy = entMan.Spawn(speciesPrototype.Prototype);

                Assert.Multiple(() =>
                {
                    Assert.That(dummy, Is.Not.EqualTo(EntityUid.Invalid), $"Failed species to pass the test: {speciesPrototype.ID}");
                    Assert.That(entMan.TryGetComponent(dummy, out ConsciousnessComponent consciousness), $"Failed species to pass the test: {speciesPrototype.ID}");

                    Assert.That(consciousnessSystem.TryGetNerveSystem(dummy, out var dummyNerveSys), Is.True);

                    Assert.That(entMan.HasComponent<OrganComponent>(dummyNerveSys), $"Failed species to pass the test: {speciesPrototype.ID}");
                    Assert.That(entMan.HasComponent<ConsciousnessRequiredComponent>(dummyNerveSys), $"Failed species to pass the test: {speciesPrototype.ID}");

                    Assert.That(consciousness.Consciousness, Is.GreaterThan(consciousness.Threshold));
                });
            }
        });

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

        var entMan = server.ResolveDependency<IEntityManager>();
        var bodySystem = entMan.System<BodySystem>();
        var woundSystem = entMan.System<WoundSystem>();
        var rejuvenateSystem = entMan.System<RejuvenateSystem>();

        await server.WaitAssertion(() =>
        {
            foreach (var speciesPrototype in server.ProtoMan.EnumeratePrototypes<SpeciesPrototype>())
            {
                if (_ignoredPrototypes.Contains(speciesPrototype.ID))
                    continue;

                var dummy = entMan.Spawn(speciesPrototype.Prototype);

                var initialBodyPartCount = bodySystem.GetBodyPartCount(dummy, BodyPartType.Head);
                var headEntity = bodySystem.GetBodyChildrenOfType(dummy, BodyPartType.Head).FirstOrDefault();
                var groinEntity = bodySystem.GetBodyChildrenOfType(dummy, BodyPartType.Groin).FirstOrDefault();

                Assert.Multiple(() =>
                {
                    Assert.That(bodySystem.TryGetParentBodyPart(headEntity.Id, out var parentPart, out _), $"Failed species to pass the test: {speciesPrototype.ID}");
                    Assert.That(parentPart, Is.Not.Null, $"Failed species to pass the test: {speciesPrototype.ID}");

                    Assert.That(entMan.TryGetComponent(headEntity.Id, out WoundableComponent woundable));

                    // Destroy the head, and damage the groin so we can check.
                    woundSystem.DestroyWoundable(parentPart.Value, headEntity.Id, woundable);
                    woundSystem.TryCreateWound(groinEntity.Id, "Blunt", 25f, "Brute");

                    rejuvenateSystem.PerformRejuvenate(dummy);

                    Assert.That(initialBodyPartCount, Is.EqualTo(bodySystem.GetBodyPartCount(dummy, BodyPartType.Head)), $"Failed species to pass the test: {speciesPrototype.ID}");

                    Assert.That(woundSystem.GetWoundableSeverityPoint(parentPart.Value), Is.GreaterThanOrEqualTo(FixedPoint2.Zero), $"Failed species to pass the test: {speciesPrototype.ID}");
                    Assert.That(woundSystem.GetWoundableSeverityPoint(groinEntity.Id), Is.GreaterThanOrEqualTo(FixedPoint2.Zero), $"Failed species to pass the test: {speciesPrototype.ID}");
                });
            }
        });

        await pair.CleanReturnAsync();
    }
}
