using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Atmos.Components;
using Content.Shared.Backmen.Body.OrganRelations;
using Content.Shared.Backmen.Body.Systems;
using Content.Shared.Backmen.Surgery.Wounds.Components;
using Content.Shared.Backmen.Surgery.Wounds.Systems;
using Content.Shared.Body;
using Content.Shared.Body.Part;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Backmen.Body;

/// <summary>
/// Heat-dominated woundable loss burns parts to ash; blunt loss does not.
/// </summary>
[TestFixture]
public sealed class PartBurnToAshTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [Test]
    public async Task HeatLossOnHand_SpawnsAshAndRemovesHand()
    {
        var map = await Pair.CreateTestMap();
        NetEntity netMob = default;
        NetEntity netHand = default;
        var ashBefore = 0;

        await Server.WaitAssertion(() =>
        {
            var entMan = Server.EntMan;
            var bodySys = entMan.System<BkmBodySharedSystem>();
            var damageable = entMan.System<DamageableSystem>();

            ashBefore = CountAshOnMap(entMan);

            var mob = entMan.SpawnEntity("MobHuman", map.MapCoords);
            Assert.That(bodySys.TryGetWoundableTargetByType(mob, BodyPartType.Hand, BodyPartSymmetry.Left, out var hand), Is.True);
            Assert.That(entMan.TryGetComponent(hand, out WoundableComponent? handWoundable), Is.True);
            netMob = entMan.GetNetEntity(mob);
            netHand = entMan.GetNetEntity(hand);

            var heat = new DamageSpecifier { DamageDict = { ["Heat"] = FixedPoint2.New(500) } };
            for (var i = 0; i < 30 && entMan.EntityExists(hand); i++)
                damageable.ChangeDamage(hand, heat, ignoreResistances: true);
        });

        await Server.WaitIdleAsync();
        await Server.WaitRunTicks(10);

        await Server.WaitAssertion(() =>
        {
            var entMan = Server.EntMan;
            var hand = entMan.GetEntity(netHand);
            var mob = entMan.GetEntity(netMob);

            Assert.That(entMan.EntityExists(mob), Is.True, "Mob should survive hand burn.");
            Assert.That(entMan.EntityExists(hand), Is.False, "Hand should be destroyed by heat loss.");
            Assert.That(CountAshOnMap(entMan), Is.GreaterThan(ashBefore), "Ash should spawn when hand burns.");
        });
    }

    [Test]
    public async Task BluntLossOnHand_DoesNotSpawnAsh()
    {
        var map = await Pair.CreateTestMap();
        NetEntity netHand = default;
        var ashBefore = 0;

        await Server.WaitAssertion(() =>
        {
            var entMan = Server.EntMan;
            var bodySys = entMan.System<BkmBodySharedSystem>();
            var damageable = entMan.System<DamageableSystem>();

            ashBefore = CountAshNear(entMan, map.MapCoords);

            var mob = entMan.SpawnEntity("MobHuman", map.MapCoords);
            Assert.That(bodySys.TryGetWoundableTargetByType(mob, BodyPartType.Hand, BodyPartSymmetry.Left, out var hand), Is.True);
            netHand = entMan.GetNetEntity(hand);

            Assert.That(entMan.TryGetComponent(hand, out WoundableComponent? handWoundable), Is.True);
            var blunt = new DamageSpecifier { DamageDict = { ["Blunt"] = FixedPoint2.New(500) } };
            for (var i = 0; i < 30 && entMan.EntityExists(hand); i++)
                damageable.ChangeDamage(hand, blunt, ignoreResistances: true);
        });

        await Server.WaitIdleAsync();
        await Server.WaitRunTicks(10);

        await Server.WaitAssertion(() =>
        {
            var entMan = Server.EntMan;
            var hand = entMan.GetEntity(netHand);

            Assert.That(entMan.EntityExists(hand), Is.False, "Hand should be destroyed by blunt loss.");
            Assert.That(CountAshNear(entMan, map.MapCoords), Is.EqualTo(ashBefore),
                "Blunt amputation should not spawn burn ash.");
        });
    }

    [Test]
    public async Task DetachedHeadHeatLoss_BurnsHeadAndPreservesBrain()
    {
        var map = await Pair.CreateTestMap();
        NetEntity netBrain = default;
        NetEntity netHead = default;
        NetEntity netBundle = default;

        await Server.WaitAssertion(() =>
        {
            var entMan = Server.EntMan;
            var bodySys = entMan.System<BkmBodySharedSystem>();
            var organRelations = entMan.System<OrganRelationInitializerSystem>();
            var woundSystem = entMan.System<WoundSystem>();

            var bundle = entMan.SpawnEntity("BackmenDetachedBody", map.MapCoords);
            var head = entMan.SpawnEntity("OrganHumanHead", MapCoordinates.Nullspace);
            var brain = entMan.SpawnEntity("OrganHumanBrain", MapCoordinates.Nullspace);

            Assert.That(bodySys.InsertOrganIntoBody(bundle, head), Is.True);
            Assert.That(bodySys.InsertOrganIntoBody(bundle, brain), Is.True);
            organRelations.WireRelationships((bundle, entMan.GetComponent<BodyComponent>(bundle)));
            Assert.That(entMan.TryGetComponent(head, out WoundableComponent? headWoundable), Is.True);

            var created = new BkmDetachedBodyCreatedEvent(bundle, bundle, BkmDetachContext.Surgery);
            entMan.EventBus.RaiseLocalEvent(bundle, ref created);

            netBrain = entMan.GetNetEntity(brain);
            netHead = entMan.GetNetEntity(head);
            netBundle = entMan.GetNetEntity(bundle);

            for (var i = 0; i < 50 && entMan.EntityExists(head); i++)
                woundSystem.TryInduceWound(head, "Heat", 200f, out _, headWoundable);
        });

        await Server.WaitIdleAsync();
        await Server.WaitRunTicks(10);

        await Server.WaitAssertion(() =>
        {
            var entMan = Server.EntMan;
            var brain = entMan.GetEntity(netBrain);
            var head = entMan.GetEntity(netHead);

            Assert.That(entMan.EntityExists(head), Is.False, "Head should burn to ash.");
            Assert.That(entMan.EntityExists(brain), Is.True, "Brain should survive head burn.");
            Assert.That(entMan.HasComponent<BkmDetachedBrainProtectionComponent>(brain), Is.True);
            Assert.That(entMan.HasComponent<FlammableComponent>(brain), Is.False,
                "Preserved brain must not be ignited.");
            Assert.That(entMan.EntityExists(entMan.GetEntity(netBundle)), Is.False, "Bundle should be consumed.");
        });
    }

    [Test]
    public async Task DetachedHeadBluntLoss_GibsWithoutAsh()
    {
        var map = await Pair.CreateTestMap();
        NetEntity netBrain = default;
        NetEntity netBundle = default;
        var ashBefore = 0;

        await Server.WaitAssertion(() =>
        {
            var entMan = Server.EntMan;
            var bodySys = entMan.System<BkmBodySharedSystem>();
            var organRelations = entMan.System<OrganRelationInitializerSystem>();
            var damageableSys = entMan.System<DamageableSystem>();

            ashBefore = CountAshOnMap(entMan);

            var bundle = entMan.SpawnEntity("BackmenDetachedBody", map.MapCoords);
            var head = entMan.SpawnEntity("OrganHumanHead", MapCoordinates.Nullspace);
            var brain = entMan.SpawnEntity("OrganHumanBrain", MapCoordinates.Nullspace);

            Assert.That(bodySys.InsertOrganIntoBody(bundle, head), Is.True);
            Assert.That(bodySys.InsertOrganIntoBody(bundle, brain), Is.True);
            organRelations.WireRelationships((bundle, entMan.GetComponent<BodyComponent>(bundle)));

            var created = new BkmDetachedBodyCreatedEvent(bundle, bundle, BkmDetachContext.Surgery);
            entMan.EventBus.RaiseLocalEvent(bundle, ref created);

            netBrain = entMan.GetNetEntity(brain);
            netBundle = entMan.GetNetEntity(bundle);

            var blunt = new DamageSpecifier { DamageDict = { ["Blunt"] = FixedPoint2.New(9999) } };
            damageableSys.TryChangeDamage(bundle, blunt, ignoreResistances: true);
        });

        await Server.WaitIdleAsync();
        await Server.WaitRunTicks(10);

        await Server.WaitAssertion(() =>
        {
            var entMan = Server.EntMan;
            var brain = entMan.GetEntity(netBrain);

            Assert.That(entMan.EntityExists(brain), Is.True, "Brain should be ejected on blunt gib.");
            Assert.That(entMan.HasComponent<BkmDetachedBrainProtectionComponent>(brain), Is.True);
            Assert.That(entMan.EntityExists(entMan.GetEntity(netBundle)), Is.False, "Bundle should be consumed.");
            Assert.That(CountAshOnMap(entMan), Is.EqualTo(ashBefore),
                "Blunt detached bundle loss should not spawn burn ash.");
        });
    }

    private static int CountAshOnMap(IEntityManager entMan)
    {
        var count = 0;
        var query = entMan.AllEntityQueryEnumerator<MetaDataComponent>();
        while (query.MoveNext(out _, out var meta))
        {
            if (meta.EntityPrototype?.ID == "Ash")
                count++;
        }

        return count;
    }

    private static int CountAshNear(IEntityManager entMan, MapCoordinates origin, float radius = 4f)
    {
        var count = 0;
        var xform = entMan.System<SharedTransformSystem>();
        var radiusSq = radius * radius;
        var query = entMan.AllEntityQueryEnumerator<MetaDataComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var meta, out var xformComp))
        {
            if (meta.EntityPrototype?.ID != "Ash")
                continue;

            if ((xform.GetMapCoordinates(uid, xformComp).Position - origin.Position).LengthSquared() <= radiusSq)
                count++;
        }

        return count;
    }
}
