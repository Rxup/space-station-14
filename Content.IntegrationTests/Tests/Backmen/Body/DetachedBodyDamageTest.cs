using System.Collections.Generic;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Atmos.Rotting;
using Content.Shared.Backmen.Body.OrganRelations;
using Content.Shared.Backmen.Body.Systems;
using Content.Shared.Body;
using Content.Shared.Body.Organ;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Backmen.Body;

/// <summary>
/// Destroying a detached bundle's root organ ejects child/internal organs; brain is immune after ejection.
/// </summary>
[TestFixture]
public sealed class DetachedBodyDamageTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [Test]
    public async Task DestroyingLegBundle_EjectsFoot()
    {
        var map = await Pair.CreateTestMap();
        NetEntity netFoot = default;

        await Server.WaitAssertion(() =>
        {
            var entMan = Server.EntMan;
            var bodySys = entMan.System<BkmBodySharedSystem>();
            var organRelations = entMan.System<OrganRelationInitializerSystem>();
            var damageableSys = entMan.System<DamageableSystem>();

            var bundle = entMan.SpawnEntity("BackmenDetachedBody", map.MapCoords);
            var leg = entMan.SpawnEntity("OrganHumanLegLeft", MapCoordinates.Nullspace);
            var foot = entMan.SpawnEntity("OrganHumanFootLeft", MapCoordinates.Nullspace);

            Assert.That(bodySys.InsertOrganIntoBody(bundle, leg), Is.True);
            Assert.That(bodySys.InsertOrganIntoBody(bundle, foot), Is.True);
            organRelations.WireRelationships((bundle, entMan.GetComponent<BodyComponent>(bundle)));

            var created = new BkmDetachedBodyCreatedEvent(bundle, bundle, BkmDetachContext.Surgery);
            entMan.EventBus.RaiseLocalEvent(bundle, ref created);

            var damage = new DamageSpecifier { DamageDict = new Dictionary<string, FixedPoint2> { { "Blunt", 9999 } } };
            damageableSys.TryChangeDamage(bundle, damage, ignoreResistances: true);

            netFoot = entMan.GetNetEntity(foot);
        });

        await Server.WaitIdleAsync();

        await Server.WaitAssertion(() =>
        {
            var entMan = Server.EntMan;
            var foot = entMan.GetEntity(netFoot);

            Assert.That(entMan.EntityExists(foot), Is.True, "Foot should survive leg bundle destruction.");
            Assert.That(entMan.HasComponent<BkmDetachedBrainProtectionComponent>(foot), Is.False);

            var inBundle = false;
            var query = entMan.EntityQueryEnumerator<BkmDetachedBodyComponent, BodyComponent>();
            while (query.MoveNext(out _, out _, out var body))
            {
                if (body.Organs?.Contains(foot) == true)
                {
                    inBundle = true;
                    break;
                }
            }

            Assert.That(inBundle, Is.False, "Foot should be ejected onto the map.");
        });
    }

    [Test]
    public async Task DestroyingHeadBundle_EjectsImmuneBrain()
    {
        var map = await Pair.CreateTestMap();
        NetEntity netBrain = default;

        await Server.WaitAssertion(() =>
        {
            var entMan = Server.EntMan;
            var bodySys = entMan.System<BkmBodySharedSystem>();
            var organRelations = entMan.System<OrganRelationInitializerSystem>();
            var damageableSys = entMan.System<DamageableSystem>();

            var bundle = entMan.SpawnEntity("BackmenDetachedBody", map.MapCoords);
            var head = entMan.SpawnEntity("OrganHumanHead", MapCoordinates.Nullspace);
            var brain = entMan.SpawnEntity("OrganHumanBrain", MapCoordinates.Nullspace);

            Assert.That(bodySys.InsertOrganIntoBody(bundle, head), Is.True);
            Assert.That(bodySys.InsertOrganIntoBody(bundle, brain), Is.True);
            organRelations.WireRelationships((bundle, entMan.GetComponent<BodyComponent>(bundle)));

            var created = new BkmDetachedBodyCreatedEvent(bundle, bundle, BkmDetachContext.Surgery);
            entMan.EventBus.RaiseLocalEvent(bundle, ref created);

            var damage = new DamageSpecifier { DamageDict = new Dictionary<string, FixedPoint2> { { "Blunt", 9999 } } };
            damageableSys.TryChangeDamage(bundle, damage, ignoreResistances: true);

            netBrain = entMan.GetNetEntity(brain);
        });

        await Server.WaitIdleAsync();

        await Server.WaitAssertion(() =>
        {
            var entMan = Server.EntMan;
            var brain = entMan.GetEntity(netBrain);
            var damageableSys = entMan.System<DamageableSystem>();

            Assert.That(entMan.EntityExists(brain), Is.True, "Brain should survive head bundle destruction.");
            Assert.That(entMan.HasComponent<BkmDetachedBrainProtectionComponent>(brain), Is.True);
            Assert.That(entMan.HasComponent<RottingComponent>(brain), Is.False);

            var heat = new DamageSpecifier { DamageDict = new Dictionary<string, FixedPoint2> { { "Heat", 500 } } };
            damageableSys.TryChangeDamage(brain, heat, ignoreResistances: true);

            Assert.That(entMan.EntityExists(brain), Is.True, "Ejected brain should be immune to damage.");
        });
    }

    [Test]
    public async Task SurgicalDetach_DoesNotUseMessyScatter()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var entMan = Server.EntMan;
            var bodySys = entMan.System<BkmBodySharedSystem>();
            var organBody = entMan.System<BodySystem>();

            var patient = entMan.SpawnEntity("MobHuman", map.MapCoords);
            Assert.That(organBody.TryGetOrganByCategory(patient, "LegLeft", out var leg), Is.True);

            var legComp = entMan.GetComponent<OrganComponent>(leg);
            bodySys.RemoveOrgan(leg, legComp);

            EntityUid? bundle = null;
            var query = entMan.EntityQueryEnumerator<BkmDetachedBodyComponent, BodyComponent>();
            while (query.MoveNext(out var uid, out _, out var body))
            {
                if (body.Organs?.Contains(leg) == true)
                {
                    bundle = uid;
                    break;
                }
            }

            Assert.That(bundle, Is.Not.Null, "Surgical leg removal should create a detached bundle.");
            Assert.That(entMan.GetComponent<BkmDetachedBodyComponent>(bundle.Value).MessyScatter, Is.False);
        });
    }

    [Test]
    public async Task ShellDamageAfterRootDestroyed_EjectsRemainingOrgans()
    {
        var map = await Pair.CreateTestMap();
        NetEntity netBrain = default;
        NetEntity netBundle = default;

        await Server.WaitAssertion(() =>
        {
            var entMan = Server.EntMan;
            var bodySys = entMan.System<BkmBodySharedSystem>();
            var organRelations = entMan.System<OrganRelationInitializerSystem>();
            var damageableSys = entMan.System<DamageableSystem>();

            var bundle = entMan.SpawnEntity("BackmenDetachedBody", map.MapCoords);
            var head = entMan.SpawnEntity("OrganHumanHead", MapCoordinates.Nullspace);
            var brain = entMan.SpawnEntity("OrganHumanBrain", MapCoordinates.Nullspace);

            Assert.That(bodySys.InsertOrganIntoBody(bundle, head), Is.True);
            Assert.That(bodySys.InsertOrganIntoBody(bundle, brain), Is.True);
            organRelations.WireRelationships((bundle, entMan.GetComponent<BodyComponent>(bundle)));

            var created = new BkmDetachedBodyCreatedEvent(bundle, bundle, BkmDetachContext.Surgery);
            entMan.EventBus.RaiseLocalEvent(bundle, ref created);

            entMan.DeleteEntity(head);
            entMan.GetComponent<BkmDetachedBodyComponent>(bundle).RootOrgan = null;

            var damage = new DamageSpecifier { DamageDict = new Dictionary<string, FixedPoint2> { { "Blunt", 100 } } };
            damageableSys.TryChangeDamage(bundle, damage, ignoreResistances: true);

            netBrain = entMan.GetNetEntity(brain);
            netBundle = entMan.GetNetEntity(bundle);
        });

        await Server.WaitIdleAsync();
        await Server.WaitRunTicks(5);

        await Server.WaitAssertion(() =>
        {
            var entMan = Server.EntMan;
            var brain = entMan.GetEntity(netBrain);

            Assert.That(entMan.EntityExists(brain), Is.True, "Brain should be ejected after shell damage.");
            Assert.That(entMan.HasComponent<BkmDetachedBrainProtectionComponent>(brain), Is.True);
            Assert.That(entMan.EntityExists(entMan.GetEntity(netBundle)), Is.False, "Empty bundle shell should be deleted.");
        });
    }
}
