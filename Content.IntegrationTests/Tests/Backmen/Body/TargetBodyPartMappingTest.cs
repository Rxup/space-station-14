using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Backmen.Body.Systems;
using Content.Shared.Backmen.Targeting;
using Content.Shared.Body;
using Content.Shared.Body.Part;
using Content.Shared.Humanoid.Prototypes;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.Backmen.Body;

[TestFixture]
public sealed class TargetBodyPartMappingTest : GameTest
{
    public override PoolSettings PoolSettings => new()
    {
        Dirty = true,
        Connected = false,
    };

    [Test]
    public async Task NubodySpeciesOrganLookupTest()
    {
        var bodySys = Server.EntMan.System<BodySystem>();
        var bkmBodySys = Server.EntMan.System<BkmBodySharedSystem>();
        var targetingSys = Server.EntMan.System<Content.Server.Backmen.Targeting.TargetingSystem>();

        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(TargetBodyPartMapping.TryGetCategory(TargetBodyPart.Chest, out var chestCat), Is.True);
                Assert.That(chestCat, Is.EqualTo("Torso"));
                Assert.That(TargetBodyPartMapping.IsComposite(TargetBodyPart.FullArms), Is.True);
                Assert.That(TargetBodyPartMapping.IsComposite(TargetBodyPart.Head), Is.False);

                var legacyGroin = (TargetBodyPart) (1 << 2);
                Assert.That(TargetBodyPartMapping.Normalize(legacyGroin), Is.EqualTo(TargetBodyPart.Chest));

                Assert.That(SurgeryBodyPartMapping.TryGetBodyPartType("ArachneAbdomen", out var abdomenType, out var abdomenSym), Is.True);
                Assert.That(abdomenType, Is.EqualTo(BodyPartType.Groin));
                Assert.That(abdomenSym, Is.EqualTo(BodyPartSymmetry.None));

                Assert.That(SurgeryBodyPartMapping.TryGetBodyPartType("ArachneFront", out var frontType, out var frontSym), Is.True);
                Assert.That(frontType, Is.EqualTo(BodyPartType.Groin));
                Assert.That(frontSym, Is.EqualTo(BodyPartSymmetry.None));
            });
        });

        var human = Server.ProtoMan.Index<SpeciesPrototype>("Human");
        EntityUid mob = default;

        await Server.WaitAssertion(() =>
        {
            mob = Server.EntMan.Spawn(human.Prototype);
        });

        await Server.WaitIdleAsync();
        await Server.WaitRunTicks(2);

        await Server.WaitAssertion(() =>
        {
            Assert.That(targetingSys.TryGetOrganForTarget(mob, TargetBodyPart.Head, out var head), Is.True);
            Assert.That(head.Comp.Category, Is.EqualTo("Head"));

            Assert.That(targetingSys.TryGetOrganForTarget(mob, TargetBodyPart.Chest, out var chest), Is.True);
            Assert.That(chest.Comp.Category, Is.EqualTo("Torso"));

            Assert.That(bodySys.TryGetOrganByCategory(mob, "LegLeft", out var leg), Is.True);
            Assert.That(targetingSys.GetTargetBodyPart(leg), Is.EqualTo(TargetBodyPart.LeftLeg));

            var arms = targetingSys.GetTargetEntities(mob, TargetBodyPart.Arms).ToList();
            Assert.That(arms, Has.Count.EqualTo(2));

            var surgeryTargets = targetingSys.GetSurgeryTargets(mob).ToList();
            Assert.That(surgeryTargets, Has.Count.EqualTo(10));
            Assert.That(surgeryTargets.All(uid => Server.EntMan.HasComponent<OrganComponent>(uid)), Is.True);

            var woundableTargets = bkmBodySys.GetWoundableTargets(mob).ToList();
            Assert.That(woundableTargets, Has.Count.EqualTo(10));
            Assert.That(woundableTargets.All(uid => Server.EntMan.HasComponent<OrganComponent>(uid)), Is.True);
            Assert.That(bkmBodySys.TryGetWoundableTargetByType(mob, BodyPartType.Head, null, out var headTarget), Is.True);
            Assert.That(headTarget, Is.EqualTo(head.Owner));
        });
    }
}
