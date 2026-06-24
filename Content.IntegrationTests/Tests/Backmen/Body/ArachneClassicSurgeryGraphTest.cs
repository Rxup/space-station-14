using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server.Backmen.Surgery;
using Content.Shared.Backmen.Body.OrganRelations;
using Content.Shared.Backmen.Body.Systems;
using Content.Shared.Backmen.Targeting;
using Content.Shared.Body;
using Content.Shared.Body.Part;
using Content.Shared.Medical.Surgery.Conditions;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Backmen.Body;

/// <summary>
/// Verifies the surgery graph for building an Arachne Classic graft on a layered humanoid
/// (amputate legs → graft segments → detach graft → reattach human legs).
/// </summary>
[TestFixture]
public sealed class ArachneClassicSurgeryGraphTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    private static readonly EntProtoId SurgeryGraftArachneFront = "SurgeryGraftArachneFront";
    private static readonly EntProtoId SurgeryGraftArachneAbdomen = "SurgeryGraftArachneAbdomen";
    private static readonly EntProtoId SurgeryGraftSpiderLegLeft = "SurgeryGraftSpiderLegLeft";
    private static readonly EntProtoId SurgeryGraftSpiderLegRight = "SurgeryGraftSpiderLegRight";
    private static readonly EntProtoId SurgeryDetachArachneGraft = "SurgeryDetachArachneGraft";
    private static readonly EntProtoId SurgeryAttachLeftLeg = "SurgeryAttachLeftLeg";
    private static readonly EntProtoId SurgeryAttachRightLeg = "SurgeryAttachRightLeg";
    private static readonly EntProtoId SurgeryRemovePart = "SurgeryRemovePart";

    [Test]
    public async Task LayeredHumanoid_HasFullArachneClassicSurgeryGraph()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var entMan = Server.EntMan;
            var bodySys = entMan.System<BkmBodySharedSystem>();
            var organBody = entMan.System<BodySystem>();
            var organRelations = entMan.System<OrganRelationInitializerSystem>();
            var surgerySys = entMan.System<SurgerySystem>();
            var targeting = entMan.System<SharedTargetingSystem>();

            var body = entMan.SpawnEntity("MobHuman", map.MapCoords);
            Assert.That(bodySys.BodySupportsArachneGraft(body), Is.True);

            Assert.That(
                targeting.TryGetEntityByBodyPartType(body, BodyPartType.Leg, BodyPartSymmetry.Left, out var leftLeg),
                Is.True);
            Assert.That(
                targeting.TryGetEntityByBodyPartType(body, BodyPartType.Leg, BodyPartSymmetry.Right, out var rightLeg),
                Is.True);
            Assert.That(
                bodySys.TryGetWoundableTargetByType(body, BodyPartType.Chest, null, out var torso),
                Is.True);

            AssertSurgeryValid(entMan, surgerySys, body, leftLeg, SurgeryRemovePart, expected: true);
            AssertSurgeryValid(entMan, surgerySys, body, rightLeg, SurgeryRemovePart, expected: true);
            AssertSurgeryValid(entMan, surgerySys, body, torso, SurgeryGraftArachneFront, expected: false);

            bodySys.RemoveOrgan(leftLeg);
            bodySys.RemoveOrgan(rightLeg);
            Assert.That(IsBothHumanLegsMissing(organBody, body), Is.True);

            AssertSurgeryValid(entMan, surgerySys, body, torso, SurgeryGraftArachneFront, expected: true);
            AssertSurgeryValid(entMan, surgerySys, body, torso, SurgeryGraftArachneAbdomen, expected: false);

            InsertGraft(entMan, bodySys, organBody, organRelations, body, "BioSynthArachneFront", "ArachneFront");
            AssertSurgeryValid(entMan, surgerySys, body, torso, SurgeryGraftArachneFront, expected: false);
            AssertSurgeryValid(entMan, surgerySys, body, torso, SurgeryGraftArachneAbdomen, expected: true);

            InsertGraft(entMan, bodySys, organBody, organRelations, body, "BioSynthArachneAbdomen", "ArachneAbdomen");
            AssertSurgeryValid(entMan, surgerySys, body, torso, SurgeryGraftSpiderLegLeft, expected: true);
            AssertSurgeryValid(entMan, surgerySys, body, torso, SurgeryGraftSpiderLegRight, expected: true);

            foreach (var slot in SurgeryBodyPartMapping.SpiderLegLeftSlots)
                InsertGraft(entMan, bodySys, organBody, organRelations, body, "BioSynthSpiderLegLeft", slot);

            foreach (var slot in SurgeryBodyPartMapping.SpiderLegRightSlots)
                InsertGraft(entMan, bodySys, organBody, organRelations, body, "BioSynthSpiderLegRight", slot);

            Assert.That(bodySys.BodyHasArachneOrgan(body), Is.True);
            foreach (var category in SurgeryBodyPartMapping.ArachneGraftInstallOrder)
                Assert.That(organBody.TryGetOrganByCategory(body, category, out _), Is.True, category.ToString());

            AssertSurgeryValid(entMan, surgerySys, body, torso, SurgeryAttachLeftLeg, expected: false);
            AssertSurgeryValid(entMan, surgerySys, body, torso, SurgeryAttachRightLeg, expected: false);

            foreach (var category in SurgeryBodyPartMapping.ArachneGraftInstallOrder.Reverse())
            {
                Assert.That(
                    organBody.TryGetOrganByCategory(body, category, out var graftPart),
                    Is.True,
                    $"Expected {category} before detach.");

                AssertSurgeryValid(entMan, surgerySys, body, graftPart, SurgeryDetachArachneGraft, expected: true);

                foreach (var other in SurgeryBodyPartMapping.ArachneGraftInstallOrder)
                {
                    if (!organBody.TryGetOrganByCategory(body, other, out _))
                        continue;

                    var canDetach = SurgeryBodyPartMapping.CanDetachArachneGraftCategory(body, other, organBody);
                    Assert.That(canDetach, Is.EqualTo(other == category), $"Detach gate for {other} while removing {category}.");
                }

                bodySys.RemoveOrgan(graftPart);
            }

            Assert.That(bodySys.BodyHasArachneOrgan(body), Is.False);
            Assert.That(IsBothHumanLegsMissing(organBody, body), Is.True);

            AssertSurgeryValid(entMan, surgerySys, body, torso, SurgeryAttachLeftLeg, expected: true);
            AssertSurgeryValid(entMan, surgerySys, body, torso, SurgeryAttachRightLeg, expected: true);
        });
    }

    [Test]
    public async Task FlatOrganMob_HasNoArachneGraftSupport()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var entMan = Server.EntMan;
            var bodySys = entMan.System<BkmBodySharedSystem>();
            var npc = entMan.SpawnEntity("MobCarp", map.MapCoords);

            Assert.That(bodySys.BodySupportsArachneGraft(npc), Is.False);
        });
    }

    private static bool IsBothHumanLegsMissing(BodySystem organBody, EntityUid body) =>
        !organBody.TryGetOrganByCategory(body, "LegLeft", out _)
        && !organBody.TryGetOrganByCategory(body, "LegRight", out _);

    private static void AssertSurgeryValid(
        IEntityManager entMan,
        SurgerySystem surgerySys,
        EntityUid body,
        EntityUid part,
        EntProtoId surgeryId,
        bool expected)
    {
        Assert.That(surgerySys.GetSingleton(surgeryId), Is.Not.Null, $"Missing surgery prototype {surgeryId}.");
        var surgeryEnt = surgerySys.GetSingleton(surgeryId)!.Value;

        var ev = new SurgeryValidEvent(body, part);
        entMan.EventBus.RaiseLocalEvent(surgeryEnt, ref ev);
        Assert.That(!ev.Cancelled, Is.EqualTo(expected), surgeryId.ToString());
    }

    private static void InsertGraft(
        IEntityManager entMan,
        BkmBodySharedSystem bodySys,
        BodySystem organBody,
        OrganRelationInitializerSystem organRelations,
        EntityUid body,
        EntProtoId graftId,
        ProtoId<OrganCategoryPrototype> category)
    {
        if (!entMan.TryGetComponent(body, out BodyComponent? bodyComp))
            return;

        if (organBody.TryGetOrganByCategory((body, bodyComp), category, out _))
            return;

        var graft = entMan.SpawnEntity(graftId, MapCoordinates.Nullspace);

        if (SurgeryBodyPartMapping.IsSpiderLegCategory(category))
            organBody.SetOrganCategory(graft, category);

        bodySys.InsertOrganIntoBody(body, graft);
        organRelations.WireGraftRelationships((body, bodyComp));
        bodySys.SyncLegEntitiesForBody((body, bodyComp));
    }
}
