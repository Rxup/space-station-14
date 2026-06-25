using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Administration.Systems;
using Content.Shared.Backmen.Body.OrganRelations;
using Content.Shared.Backmen.Body.Systems;
using Content.Shared.Backmen.Targeting;
using Content.Shared.Body;
using Content.Shared.Standing;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Backmen.Body;

/// <summary>
/// graftarachne → rejuvenate → graftarachne must keep the arachne graft intact.
/// Rejuvenation should not respawn human legs/feet on grafted bodies.
/// </summary>
[TestFixture]
public sealed class GraftArachneRejuvenateTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    /// <summary>
    /// Mirrors <c>graftarachne</c> admin command behavior.
    /// </summary>
    private static void GraftArachne(IEntityManager entMan, EntityUid body)
    {
        var bodySys = entMan.System<BkmBodySharedSystem>();
        var organBody = entMan.System<BodySystem>();
        var organRelations = entMan.System<OrganRelationInitializerSystem>();

        if (!bodySys.BodySupportsArachneGraft(body)
            || !entMan.TryGetComponent(body, out BodyComponent? bodyComp))
            return;

        foreach (var category in new ProtoId<OrganCategoryPrototype>[] { "LegLeft", "LegRight" })
        {
            if (organBody.TryGetOrganByCategory((body, bodyComp), category, out var leg))
                bodySys.RemoveOrgan(leg);
        }

        TryInsertGraft(entMan, bodySys, organBody, body, "BioSynthArachneFront", "ArachneFront");
        TryInsertGraft(entMan, bodySys, organBody, body, "BioSynthArachneAbdomen", "ArachneAbdomen");
        InsertSpiderLegs(entMan, bodySys, organBody, body, SurgeryBodyPartMapping.SpiderLegLeftSlots, "BioSynthSpiderLegLeft");
        InsertSpiderLegs(entMan, bodySys, organBody, body, SurgeryBodyPartMapping.SpiderLegRightSlots, "BioSynthSpiderLegRight");

        if (entMan.TryGetComponent(body, out bodyComp))
        {
            organRelations.WireGraftRelationships((body, bodyComp));
            bodySys.SyncLegEntitiesForBody((body, bodyComp));
        }
    }

    private static void TryInsertGraft(
        IEntityManager entMan,
        BkmBodySharedSystem bodySys,
        BodySystem organBody,
        EntityUid body,
        EntProtoId graftId,
        ProtoId<OrganCategoryPrototype> category)
    {
        if (!entMan.TryGetComponent(body, out BodyComponent? bodyComp))
            return;

        if (organBody.TryGetOrganByCategory((body, bodyComp), category, out _))
            return;

        var graft = entMan.SpawnEntity(graftId, MapCoordinates.Nullspace);
        bodySys.InsertOrganIntoBody(body, graft);
    }

    private static void InsertSpiderLegs(
        IEntityManager entMan,
        BkmBodySharedSystem bodySys,
        BodySystem organBody,
        EntityUid body,
        ProtoId<OrganCategoryPrototype>[] slots,
        EntProtoId legGraftId)
    {
        if (!entMan.TryGetComponent(body, out BodyComponent? bodyComp))
            return;

        foreach (var slot in slots)
        {
            if (organBody.TryGetOrganByCategory((body, bodyComp), slot, out _))
                continue;

            var leg = entMan.SpawnEntity(legGraftId, MapCoordinates.Nullspace);
            organBody.SetOrganCategory(leg, slot);
            bodySys.InsertOrganIntoBody(body, leg);
        }
    }

    private static void AssertGraftedArachneIntact(IEntityManager entMan, EntityUid body, string phase)
    {
        var bodySys = entMan.System<BkmBodySharedSystem>();
        var organBody = entMan.System<BodySystem>();
        var bodyComp = entMan.GetComponent<BodyComponent>(body);

        Assert.That(bodySys.BodyHasArachneOrgan(body), Is.True, $"{phase}: body should have arachne graft organs.");

        Assert.That(
            organBody.TryGetOrganByCategory((body, bodyComp), "Torso", out _),
            Is.True,
            $"{phase}: torso must remain.");

        Assert.That(
            organBody.TryGetOrganByCategory((body, bodyComp), "ArachneAbdomen", out _),
            Is.True,
            $"{phase}: arachne abdomen must remain.");

        Assert.That(
            organBody.TryGetOrganByCategory((body, bodyComp), "ArachneFront", out _),
            Is.True,
            $"{phase}: arachne front must remain.");

        foreach (var slot in SurgeryBodyPartMapping.SpiderLegLeftSlots
                     .Concat(SurgeryBodyPartMapping.SpiderLegRightSlots))
        {
            Assert.That(
                organBody.TryGetOrganByCategory((body, bodyComp), slot, out _),
                Is.True,
                $"{phase}: missing spider leg slot {slot}.");
        }

        Assert.That(
            organBody.TryGetOrganByCategory((body, bodyComp), "LegLeft", out _),
            Is.False,
            $"{phase}: human left leg should not be present on grafted arachne.");

        Assert.That(
            organBody.TryGetOrganByCategory((body, bodyComp), "LegRight", out _),
            Is.False,
            $"{phase}: human right leg should not be present on grafted arachne.");

        Assert.That(
            organBody.TryGetOrganByCategory((body, bodyComp), "FootLeft", out _),
            Is.False,
            $"{phase}: human left foot should not be present on grafted arachne.");

        Assert.That(
            organBody.TryGetOrganByCategory((body, bodyComp), "FootRight", out _),
            Is.False,
            $"{phase}: human right foot should not be present on grafted arachne.");

        Assert.That(
            bodyComp.LegEntities.Count,
            Is.EqualTo(SurgeryBodyPartMapping.ArachneRequiredLegCount),
            $"{phase}: spider legs must be tracked for movement.");

        var standAttempt = new StandAttemptEvent();
        entMan.EventBus.RaiseLocalEvent(body, standAttempt);
        Assert.That(standAttempt.Cancelled, Is.False, $"{phase}: grafted arachne should be able to stand.");
    }

    [Test]
    public async Task GraftArachne_Rejuvenate_GraftArachne_KeepsSpiderBody()
    {
        var map = await Pair.CreateTestMap();
        NetEntity netMob = default;

        await Server.WaitAssertion(() =>
        {
            var mob = Server.EntMan.SpawnEntity("MobHuman", map.MapCoords);
            netMob = Server.EntMan.GetNetEntity(mob);

            GraftArachne(Server.EntMan, mob);
            AssertGraftedArachneIntact(Server.EntMan, mob, "After first graftarachne");
        });

        await Server.WaitIdleAsync();

        await Server.WaitAssertion(() =>
        {
            var mob = Server.EntMan.GetEntity(netMob);
            Server.EntMan.System<RejuvenateSystem>().PerformRejuvenate(mob);
            AssertGraftedArachneIntact(Server.EntMan, mob, "After rejuvenate");
        });

        await Server.WaitIdleAsync();

        await Server.WaitAssertion(() =>
        {
            var mob = Server.EntMan.GetEntity(netMob);
            GraftArachne(Server.EntMan, mob);
            AssertGraftedArachneIntact(Server.EntMan, mob, "After second graftarachne");
        });
    }

    [Test]
    public async Task GraftArachne_RejectsFlatOrganNpc()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var bodySys = Server.EntMan.System<BkmBodySharedSystem>();
            var organBody = Server.EntMan.System<BodySystem>();

            var human = Server.EntMan.SpawnEntity("MobHuman", map.MapCoords);
            // Flat external organs (Torso/Head) but no VisualBodyComponent — unlike layered humanoids.
            var npc = Server.EntMan.SpawnEntity("MobCarp", map.MapCoords);

            Assert.That(bodySys.BodySupportsArachneGraft(human), Is.True);
            Assert.That(bodySys.BodySupportsArachneGraft(npc), Is.False);

            Assert.That(Server.EntMan.TryGetComponent(npc, out BodyComponent? npcBody), Is.True);
            var legCountBefore = npcBody!.LegEntities.Count;
            Assert.That(
                organBody.TryGetOrganByCategory((npc, npcBody), "Torso", out var torsoBefore),
                Is.True);

            GraftArachne(Server.EntMan, npc);

            Assert.That(bodySys.BodyHasArachneOrgan(npc), Is.False);
            Assert.That(npcBody.LegEntities.Count, Is.EqualTo(legCountBefore));
            Assert.That(
                organBody.TryGetOrganByCategory((npc, npcBody), "Torso", out var torsoAfter),
                Is.True);
            Assert.That(torsoAfter, Is.EqualTo(torsoBefore));
            Assert.That(
                organBody.TryGetOrganByCategory((npc, npcBody), "ArachneAbdomen", out _),
                Is.False);
        });
    }
}
