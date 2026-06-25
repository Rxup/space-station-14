using Content.IntegrationTests.Fixtures;
using Content.Shared.Backmen.Body.OrganRelations;
using Content.Shared.Backmen.Body.Systems;
using Content.Shared.Backmen.Targeting;
using Content.Shared.Body;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Backmen.Body;

[TestFixture]
public sealed class ArachneGraftDetachOrderTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [Test]
    public async Task ArachneGraft_RemovalFollowsReverseInstallOrder()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var entMan = Server.EntMan;
            var bodySys = entMan.System<BkmBodySharedSystem>();
            var organBody = entMan.System<BodySystem>();
            var organRelations = entMan.System<OrganRelationInitializerSystem>();
            var body = entMan.SpawnEntity("MobHuman", map.MapCoords);

            Assert.That(
                SurgeryBodyPartMapping.TryGetLastAttachedArachneGraft(body, organBody, out _),
                Is.False);

            foreach (var category in SurgeryBodyPartMapping.ArachneGraftInstallOrder)
            {
                var graftId = category.Id switch
                {
                    "ArachneFront" => "BioSynthArachneFront",
                    "ArachneAbdomen" => "BioSynthArachneAbdomen",
                    _ when SurgeryBodyPartMapping.IsSpiderLegCategory(category) => category.Id.StartsWith("SpiderLegLeft")
                        ? "BioSynthSpiderLegLeft"
                        : "BioSynthSpiderLegRight",
                    _ => throw new InvalidOperationException(),
                };

                InsertGraft(entMan, bodySys, organBody, organRelations, body, graftId, category);

                Assert.That(
                    SurgeryBodyPartMapping.TryGetLastAttachedArachneGraft(body, organBody, out var last),
                    Is.True,
                    $"Expected a removable graft after inserting {category}.");

                Assert.That(last, Is.EqualTo(category));

                foreach (var other in SurgeryBodyPartMapping.ArachneGraftInstallOrder)
                {
                    if (!organBody.TryGetOrganByCategory(body, other, out _))
                        continue;

                    var canDetach = SurgeryBodyPartMapping.CanDetachArachneGraftCategory(body, other, organBody);
                    Assert.That(
                        canDetach,
                        Is.EqualTo(other == category),
                        $"After inserting {category}, detach eligibility for {other} was unexpected.");
                }
            }
        });
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
