using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server.Administration.Systems;
using Content.Server.Body.Systems;
using Content.Server.Hands.Systems;
using Content.Server.Tools.Innate;
using Content.Shared.Administration.Systems;
using Content.Shared.Backmen.Surgery.Consciousness.Components;
using Content.Shared.Backmen.Surgery.Consciousness.Systems;
using Content.Shared.Backmen.Surgery.Pain.Components;
using Content.Shared.Backmen.Surgery.Traumas.Components;
using Content.Shared.Backmen.Surgery.Wounds.Components;
using Content.Shared.Backmen.Surgery.Wounds.Systems;
using Content.Shared.Body;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Part;
using Content.Shared.FixedPoint;
using Content.Shared.Humanoid.Prototypes;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests.Backmen.Body;

[TestFixture]
public sealed class BodySetupTest : GameTest
{
    /// <summary>
    /// A list of species that can be ignored by this test.
    /// </summary>
    private readonly HashSet<string> _ignoredPrototypes = new()
    {
        "Skeleton",
        "Monkey",
    };

    public override PoolSettings PoolSettings => new()
    {
        Dirty = true,
        Connected = true,
        InLobby = false,
    };

    [Test]
    public async Task InnateToolTest()
    {
        var prototypeManager = Server.ResolveDependency<IPrototypeManager>();
        var compFactory = Server.ResolveDependency<IComponentFactory>();

        var handsSys = Server.EntMan.System<HandsSystem>();

        var testMap = await Pair.CreateTestMap();

        //var ticker = server.System<GameTicker>();
        //Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.InRound));

        foreach (var proto in prototypeManager.EnumeratePrototypes<EntityPrototype>())
        {
            var skip = false;
            InnateToolComponent toolComponent = null;
            await Server.WaitAssertion(() =>
            {
                if (!proto.TryGetComponent(out toolComponent, compFactory))
                    skip = true;
            });

            if(skip)
                continue;

            var dummy = EntityUid.Invalid;
            await Server.WaitAssertion(() =>
            {
                dummy = Server.EntMan.Spawn(proto.ID, testMap.MapCoords);
            });
            await Server.WaitIdleAsync();
            await Server.WaitRunTicks(2);
            await Server.WaitAssertion(() =>
            {
                Assert.That(dummy, Is.Not.EqualTo(EntityUid.Invalid));
                var handCount = handsSys.EnumerateHands(dummy).Count();
                Assert.That(handCount, Is.GreaterThanOrEqualTo(toolComponent.Tools.Count), $"hands {proto.ID}");
                Server.EntMan.DeleteEntity(dummy);
            });
        }
    }

    [Test]
    public async Task AllSpeciesHaveLegs()
    {
        var bodySys = Server.EntMan.System<BodySystem>();

        foreach (var speciesPrototype in Server.ProtoMan.EnumeratePrototypes<SpeciesPrototype>())
        {
            var dummy = EntityUid.Invalid;
            await Server.WaitAssertion(() =>
            {
                dummy = Server.EntMan.Spawn(speciesPrototype.Prototype);
            });
            await Server.WaitIdleAsync();
            await Server.WaitRunTicks(2);
            await Server.WaitAssertion(() =>
            {
                Assert.That(dummy, Is.Not.EqualTo(EntityUid.Invalid));
                var bodyComp = Server.EntMan.GetComponent<BodyComponent>(dummy);
                var legs = bodyComp.LegEntities;
                var legsCount = bodySys.GetBodyPartCount(dummy, BodyPartType.Leg);
                Assert.That(legsCount, Is.EqualTo(legs.Count));
                Assert.That(legsCount, Is.GreaterThanOrEqualTo(2), $"legs {speciesPrototype.ID}({speciesPrototype.Prototype})");
            });

        }
    }

    [Test]
    public async Task AllSpeciesHaveHands()
    {
        var handsSys = Server.EntMan.System<HandsSystem>();

        foreach (var speciesPrototype in Server.ProtoMan.EnumeratePrototypes<SpeciesPrototype>())
        {
            var dummy = EntityUid.Invalid;
            await Server.WaitAssertion(() =>
            {
                dummy = Server.EntMan.Spawn(speciesPrototype.Prototype);
            });
            await Server.WaitIdleAsync();
            await Server.WaitRunTicks(2);
            await Server.WaitAssertion(() =>
            {
                Assert.That(dummy, Is.Not.EqualTo(EntityUid.Invalid));
                var handCount = handsSys.EnumerateHands(dummy).Count();
                Assert.That(handCount, Is.GreaterThanOrEqualTo(2), $"hands {speciesPrototype.ID}({speciesPrototype.Prototype})");
            });

        }
    }

    [Test]
    public async Task AllSpeciesAreConscious()
    {
        var entMan = Server.ResolveDependency<IEntityManager>();
        var consciousnessSystem = entMan.System<ConsciousnessSystem>();

        await Server.WaitAssertion(() =>
        {
            foreach (var speciesPrototype in Server.ProtoMan.EnumeratePrototypes<SpeciesPrototype>())
            {
                if (_ignoredPrototypes.Contains(speciesPrototype.ID))
                    continue;

                var dummy = entMan.Spawn(speciesPrototype.Prototype);

                Assert.Multiple(() =>
                {
                    Assert.That(dummy, Is.Not.EqualTo(EntityUid.Invalid), $"Failed species to pass the test: {speciesPrototype.ID}");
                    Assert.That(entMan.TryGetComponent(dummy, out ConsciousnessComponent consciousness), $"Failed species to pass the test: {speciesPrototype.ID}");

                    Assert.That(consciousnessSystem.TryGetNerveSystem(dummy, out var dummyNerveSys));

                    Assert.That(entMan.HasComponent<OrganComponent>(dummyNerveSys), $"Failed species to pass the test: {speciesPrototype.ID}");
                    Assert.That(entMan.HasComponent<ConsciousnessRequiredComponent>(dummyNerveSys), $"Failed species to pass the test: {speciesPrototype.ID}");

                    Assert.That(consciousnessSystem.CheckConscious((dummy, consciousness)), $"Failed species to pass the test: {speciesPrototype.ID}");
                });
            }
        });
    }

    [Test]
    public async Task AllSpeciesCanBeRejuvenated()
    {
        var entMan = Server.ResolveDependency<IEntityManager>();
        var bodySystem = entMan.System<BodySystem>();
        var woundSystem = entMan.System<WoundSystem>();
        var consciousnessSystem = entMan.System<ConsciousnessSystem>();
        var rejuvenateSystem = entMan.System<RejuvenateSystem>();

        await Server.WaitAssertion(() =>
        {
            foreach (var speciesPrototype in Server.ProtoMan.EnumeratePrototypes<SpeciesPrototype>())
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

                    Assert.That(entMan.TryGetComponent(headEntity.Id, out WoundableComponent woundable), $"Failed species to pass the test: {speciesPrototype.ID}");
                    Assert.That(entMan.TryGetComponent(groinEntity.Id, out WoundableComponent groinWoundable), $"Failed species to pass the test: {speciesPrototype.ID}");

                    // Destroy the head, and damage the groin so we can check.
                    woundSystem.DestroyWoundable(parentPart.Value, headEntity.Id, woundable);
                    woundSystem.TryInduceWound(groinEntity.Id, "Blunt", 25f, out _, groinWoundable);

                    rejuvenateSystem.PerformRejuvenate(dummy);

                    Assert.That(initialBodyPartCount, Is.EqualTo(bodySystem.GetBodyPartCount(dummy, BodyPartType.Head)), $"Failed species to pass the test: {speciesPrototype.ID}");

                    Assert.That(woundSystem.GetWoundableSeverityPoint(parentPart.Value), Is.GreaterThanOrEqualTo(FixedPoint2.Zero), $"Failed species to pass the test: {speciesPrototype.ID}");
                    Assert.That(woundSystem.GetWoundableSeverityPoint(groinEntity.Id), Is.GreaterThanOrEqualTo(FixedPoint2.Zero), $"Failed species to pass the test: {speciesPrototype.ID}");

                    Assert.That(consciousnessSystem.CheckConscious(dummy), $"Failed species to pass the test: {speciesPrototype.ID}");
                });
            }
        });
    }

    [Test]
    public async Task AllSpeciesHaveValidWoundables()
    {
        var entMan = Server.ResolveDependency<IEntityManager>();
        var bodySystem = entMan.System<BodySystem>();
        var woundSystem = entMan.System<WoundSystem>();

        await Server.WaitAssertion(() =>
        {
            foreach (var speciesPrototype in Server.ProtoMan.EnumeratePrototypes<SpeciesPrototype>())
            {
                if (_ignoredPrototypes.Contains(speciesPrototype.ID))
                    continue;

                var dummy = entMan.Spawn(speciesPrototype.Prototype);
                foreach (var bodyPart in bodySystem.GetBodyChildren(dummy))
                {
                    Assert.Multiple(() =>
                    {
                        Assert.That(entMan.HasComponent<NerveComponent>(bodyPart.Id));
                        Assert.That(entMan.TryGetComponent(bodyPart.Id, out WoundableComponent woundable));

                        var bone = woundable.Bone.ContainedEntities.FirstOrNull();
                        Assert.That(bone, Is.Not.Null);
                        Assert.That(entMan.HasComponent<BoneComponent>(bone));
                    });

                    woundSystem.TryInduceWound(bodyPart.Id, "Piercing", 5f, out _);
                    Assert.That(woundSystem.GetWoundableSeverityPoint(bodyPart.Id), Is.EqualTo(FixedPoint2.New(5f)));
                }
            }
        });
    }
}
