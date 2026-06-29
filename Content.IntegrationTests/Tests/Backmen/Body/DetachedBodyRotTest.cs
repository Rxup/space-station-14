using System.Collections.Generic;
using Content.IntegrationTests.Fixtures;
using Content.Server.Atmos.Rotting;
using Content.Server.Backmen.Body.Systems;
using Content.Shared.Atmos.Rotting;
using Content.Shared.Backmen.Body.OrganRelations;
using Content.Shared.Backmen.Body.Systems;
using Content.Shared.Body;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests.Backmen.Body;

/// <summary>
/// Detached bundles inherit rot state; destroying a rotting head bundle ejects a non-rotting brain.
/// </summary>
[TestFixture]
public sealed class DetachedBodyRotTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [Test]
    public async Task ViolentDetach_TransfersPerishableToBundle()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var entMan = Server.EntMan;
            var bodySys = entMan.System<BkmBodySystem>();

            var patient = entMan.SpawnEntity("MobHuman", map.MapCoords);
            bodySys.GibBody(patient, gibOrgans: true);
        });

        await Server.WaitIdleAsync();

        await Server.WaitAssertion(() =>
        {
            var found = false;
            var query = Server.EntMan.EntityQueryEnumerator<BkmDetachedBodyComponent, PerishableComponent>();
            while (query.MoveNext(out _, out _, out var perishable))
            {
                found = true;
                Assert.That(perishable.ForceRotProgression, Is.True);
            }

            Assert.That(found, Is.True, "Gib should leave perishable detached bundles.");
        });
    }

    [Test]
    public async Task RottingHeadBundle_EjectsBrainWithoutRot()
    {
        var map = await Pair.CreateTestMap();
        NetEntity netBrain = default;

        await Server.WaitAssertion(() =>
        {
            var entMan = Server.EntMan;
            var bodySys = entMan.System<BkmBodySharedSystem>();
            var organRelations = entMan.System<OrganRelationInitializerSystem>();
            var damageableSys = entMan.System<DamageableSystem>();
            var rottingSys = entMan.System<RottingSystem>();

            var bundle = entMan.SpawnEntity("BackmenDetachedBody", map.MapCoords);
            var head = entMan.SpawnEntity("OrganHumanHead", MapCoordinates.Nullspace);
            var brain = entMan.SpawnEntity("OrganHumanBrain", MapCoordinates.Nullspace);

            Assert.That(bodySys.InsertOrganIntoBody(bundle, head), Is.True);
            Assert.That(bodySys.InsertOrganIntoBody(bundle, brain), Is.True);
            organRelations.WireRelationships((bundle, entMan.GetComponent<BodyComponent>(bundle)));

            rottingSys.TransferRotToDetachedBody(bundle, bundle);
            entMan.EnsureComponent<RottingComponent>(bundle);

            var created = new BkmDetachedBodyCreatedEvent(bundle, bundle, BkmDetachContext.Violent);
            entMan.EventBus.RaiseLocalEvent(bundle, ref created);

            var damage = new DamageSpecifier { DamageDict = { ["Blunt"] = FixedPoint2.New(9999) } };
            damageableSys.TryChangeDamage(bundle, damage, ignoreResistances: true);

            netBrain = entMan.GetNetEntity(brain);
        });

        await Server.WaitIdleAsync();

        await Server.WaitAssertion(() =>
        {
            var brain = Server.EntMan.GetEntity(netBrain);

            Assert.That(Server.EntMan.EntityExists(brain), Is.True);
            Assert.That(Server.EntMan.HasComponent<RottingComponent>(brain), Is.False);
            Assert.That(Server.EntMan.HasComponent<BkmDetachedBrainProtectionComponent>(brain), Is.True);
        });
    }
}
