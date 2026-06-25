using Content.IntegrationTests.Fixtures;
using Content.Shared.Backmen.Body.OrganRelations;
using Content.Shared.Backmen.Body.Systems;
using Content.Shared.Backmen.Surgery;
using Content.Shared.Body;
using Content.Shared.Buckle;
using Content.Shared.Buckle.Components;
using Content.Shared.Humanoid;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Backmen.Body;

/// <summary>
/// Ensures grafted and roundstart arachne draw spider body segments behind the humanoid torso layer,
/// including when the patient is buckled to an operating table (surgery pose).
/// </summary>
[TestFixture]
public sealed class ArachneSpriteLayerTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    /// <summary>
    /// Lower sprite layer index = drawn earlier = behind. Spider segments must sit below chest in the stack.
    /// </summary>
    private static void AssertSpiderSegmentsDrawBeforeTorso(IEntityManager entMan, EntityUid body)
    {
        var spriteSys = entMan.System<SpriteSystem>();
        Assert.That(entMan.TryGetComponent(body, out SpriteComponent? sprite), Is.True);
        var ent = (body, sprite!);

        Assert.That(spriteSys.LayerMapTryGet(ent, HumanoidVisualLayers.Chest, out var chest, false), Is.True,
            "Body sprite should have a chest layer.");
        Assert.That(spriteSys.LayerMapTryGet(ent, HumanoidVisualLayers.LLeg, out var lLeg, false), Is.True,
            "Body sprite should have an LLeg layer.");
        Assert.That(spriteSys.LayerMapTryGet(ent, HumanoidVisualLayers.RLeg, out var rLeg, false), Is.True,
            "Body sprite should have an RLeg layer.");

        var indices = $"LLeg={lLeg}, RLeg={rLeg}, Chest={chest}";
        Assert.That(lLeg, Is.LessThan(chest),
            $"Abdomen (LLeg) must draw before torso (lower index = behind). Layer indices: {indices}");
        Assert.That(rLeg, Is.LessThan(chest),
            $"Cephalothorax (RLeg) must draw before torso (lower index = behind). Layer indices: {indices}");
    }

    private static void AssertBuckledToOperatingTable(IEntityManager entMan, EntityUid body)
    {
        Assert.That(entMan.TryGetComponent(body, out BuckleComponent? buckle), Is.True);
        Assert.That(buckle!.Buckled, Is.True, "Patient should be buckled to the operating table.");
        Assert.That(entMan.HasComponent<OperatingTableComponent>(buckle.BuckledTo!.Value), Is.True);
    }

    private static void RemoveHumanLegs(IEntityManager entMan, EntityUid body)
    {
        var bodySys = entMan.System<BkmBodySharedSystem>();
        var organBody = entMan.System<BodySystem>();

        if (!entMan.TryGetComponent(body, out BodyComponent? bodyComp))
            return;

        foreach (var category in new ProtoId<OrganCategoryPrototype>[] { "LegLeft", "LegRight" })
        {
            if (organBody.TryGetOrganByCategory((body, bodyComp), category, out var leg))
                Assert.That(bodySys.RemoveOrgan(leg), Is.True, $"Failed to remove {category}.");
        }
    }

    private static void InsertArachneGraft(IEntityManager entMan, EntityUid body, EntProtoId graftId)
    {
        var bodySys = entMan.System<BkmBodySharedSystem>();
        var organRelations = entMan.System<OrganRelationInitializerSystem>();

        var graft = entMan.SpawnEntity(graftId, MapCoordinates.Nullspace);
        Assert.That(bodySys.InsertOrganIntoBody(body, graft), Is.True,
            $"Failed to insert graft {graftId}.");

        if (entMan.TryGetComponent(body, out BodyComponent? bodyComp))
            organRelations.WireGraftRelationships((body, bodyComp));
    }

    private static void FaceSouth(IEntityManager entMan, EntityUid entity)
    {
        entMan.System<SharedTransformSystem>().SetLocalRotation(entity, Direction.South.ToAngle());
    }

    private static void AssertFacingSouth(IEntityManager entMan, EntityUid body)
    {
        var xform = entMan.GetComponent<TransformComponent>(body);
        Assert.That(xform.LocalRotation.GetCardinalDir(), Is.EqualTo(Direction.South),
            "Patient should face south (towards camera).");
    }

    private static void BuckleToOperatingTable(
        IEntityManager entMan,
        SharedBuckleSystem buckleSys,
        EntityUid patient,
        MapCoordinates coords)
    {
        var table = entMan.SpawnEntity("OperatingTable", coords);
        Assert.That(buckleSys.TryBuckle(patient, patient, table), Is.True,
            "Failed to buckle patient to operating table.");
    }

    [Test]
    public async Task RoundstartArachneClassic_SpiderLayersBeforeTorso_OnOperatingTable()
    {
        var map = await Pair.CreateTestMap();
        NetEntity netMob = default;
        var buckleSys = Server.System<SharedBuckleSystem>();

        await Server.WaitAssertion(() =>
        {
            var mob = Server.EntMan.SpawnEntity("MobArachneClassic", map.MapCoords);
            FaceSouth(Server.EntMan, mob);
            netMob = Server.EntMan.GetNetEntity(mob);
            BuckleToOperatingTable(Server.EntMan, buckleSys, mob, map.MapCoords);
        });

        await Pair.RunTicksSync(15);

        await Client.WaitAssertion(() =>
        {
            var mob = Client.EntMan.GetEntity(netMob);
            AssertFacingSouth(Client.EntMan, mob);
            AssertBuckledToOperatingTable(Client.EntMan, mob);
            AssertSpiderSegmentsDrawBeforeTorso(Client.EntMan, mob);
        });
    }

    [TestCase("MobHuman")]
    [TestCase("MobDwarf")]
    [TestCase("MobVulpkanin")]
    public async Task GraftedSpecies_SpiderLayersBeforeTorso_OnOperatingTable(string mobPrototype)
    {
        var map = await Pair.CreateTestMap();
        NetEntity netMob = default;
        var buckleSys = Server.System<SharedBuckleSystem>();

        await Server.WaitAssertion(() =>
        {
            var mob = Server.EntMan.SpawnEntity(mobPrototype, map.MapCoords);
            FaceSouth(Server.EntMan, mob);
            netMob = Server.EntMan.GetNetEntity(mob);

            RemoveHumanLegs(Server.EntMan, mob);
            InsertArachneGraft(Server.EntMan, mob, "BioSynthArachneFront");
            BuckleToOperatingTable(Server.EntMan, buckleSys, mob, map.MapCoords);
        });

        await Pair.RunTicksSync(15);

        await Client.WaitAssertion(() =>
        {
            var mob = Client.EntMan.GetEntity(netMob);
            AssertFacingSouth(Client.EntMan, mob);
            AssertBuckledToOperatingTable(Client.EntMan, mob);
            AssertSpiderSegmentsDrawBeforeTorso(Client.EntMan, mob);
        });

        await Server.WaitAssertion(() =>
        {
            var mob = Server.EntMan.GetEntity(netMob);
            InsertArachneGraft(Server.EntMan, mob, "BioSynthArachneAbdomen");
        });

        await Pair.RunTicksSync(15);

        await Client.WaitAssertion(() =>
        {
            var mob = Client.EntMan.GetEntity(netMob);
            AssertFacingSouth(Client.EntMan, mob);
            AssertBuckledToOperatingTable(Client.EntMan, mob);
            AssertSpiderSegmentsDrawBeforeTorso(Client.EntMan, mob);
        });
    }
}
