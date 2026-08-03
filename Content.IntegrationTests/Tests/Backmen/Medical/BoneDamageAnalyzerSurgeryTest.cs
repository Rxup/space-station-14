using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server.Backmen.Surgery.Trauma.Systems;
using Content.Server.Medical;
using Content.Shared.Backmen.Body.Systems;
using Content.Shared.Backmen.Surgery.Traumas;
using Content.Shared.Backmen.Surgery.Traumas.Components;
using Content.Shared.Backmen.Surgery.Wounds.Components;
using Content.Shared.Backmen.Targeting;
using Content.Shared.Body.Part;
using Content.Shared.FixedPoint;
using Content.Shared.Medical.Surgery.Conditions;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests.Backmen.Medical;

[TestFixture]
public sealed class BoneDamageAnalyzerSurgeryTest : GameTest
{
    private static readonly EntProtoId MobHuman = "MobHuman";
    private static readonly EntProtoId MendBonesSurgery = "SurgeryMendBones";

    public override PoolSettings PoolSettings => new()
    {
        Connected = false,
        Dirty = true,
    };

    private static bool TryGetBone(
        IEntityManager entMan,
        BkmBodySharedSystem bodySys,
        EntityUid body,
        BodyPartType partType,
        BodyPartSymmetry? symmetry,
        out EntityUid woundable,
        out EntityUid bone)
    {
        woundable = default;
        bone = default;

        if (!bodySys.TryGetWoundableTargetByType(body, partType, symmetry, out woundable))
            return false;

        if (!entMan.TryGetComponent(woundable, out WoundableComponent? woundableComp))
            return false;

        var boneEnt = woundableComp.Bone.ContainedEntities.FirstOrNull();
        if (boneEnt == null || !entMan.HasComponent<BoneComponent>(boneEnt.Value))
            return false;

        bone = boneEnt.Value;
        return true;
    }

    [Test]
    public async Task Analyzer_ReportsBrokenBoneByBodyPart()
    {
        var map = await Pair.CreateTestMap();
        EntityUid human = default;

        await Server.WaitPost(() =>
        {
            var entMan = Server.EntMan;
            var bodySys = entMan.System<BkmBodySharedSystem>();
            var trauma = entMan.System<ServerTraumaSystem>();

            human = entMan.SpawnAtPosition(MobHuman, map.GridCoords);
            Assert.That(
                TryGetBone(entMan, bodySys, human, BodyPartType.Leg, BodyPartSymmetry.Left, out _, out var bone),
                Is.True,
                "Human left leg should have a bone entity.");

            Assert.That(trauma.SetBoneIntegrity(bone, FixedPoint2.Zero), Is.True);
        });

        await Pair.RunTicksSync(5);

        await Server.WaitAssertion(() =>
        {
            var state = Server.EntMan.System<HealthAnalyzerSystem>().GetHealthAnalyzerUiState(human);
            Assert.That(state.BoneAlerts, Is.Not.Null);
            Assert.That(
                state.BoneAlerts!.Any(a => a.BodyPart == TargetBodyPart.LeftLeg && a.Severity == BoneSeverity.Broken),
                Is.True,
                "Analyzer should report a broken left-leg bone.");
        });
    }

    [Test]
    public async Task MendBones_ValidWhenBoneDamagedWithoutTraumaEntity()
    {
        var map = await Pair.CreateTestMap();
        EntityUid human = default;
        EntityUid leg = default;

        await Server.WaitPost(() =>
        {
            var entMan = Server.EntMan;
            var bodySys = entMan.System<BkmBodySharedSystem>();
            var trauma = entMan.System<ServerTraumaSystem>();

            human = entMan.SpawnAtPosition(MobHuman, map.GridCoords);
            Assert.That(
                TryGetBone(entMan, bodySys, human, BodyPartType.Leg, BodyPartSymmetry.Left, out leg, out var bone),
                Is.True);

            Assert.That(trauma.SetBoneIntegrity(bone, FixedPoint2.Zero), Is.True);
            Assert.That(trauma.HasWoundableTrauma(leg, TraumaType.BoneDamage), Is.False,
                "Test setup should have bone damage without a lingering BoneDamage trauma entity.");
            Assert.That(trauma.HasWoundableBoneDamage(leg), Is.True);
        });

        await Pair.RunTicksSync(5);

        await Server.WaitAssertion(() =>
        {
            var surgery = Server.EntMan.Spawn(MendBonesSurgery);
            var validEv = new SurgeryValidEvent(human, leg);
            Server.EntMan.EventBus.RaiseLocalEvent(surgery, ref validEv);
            Assert.That(validEv.Cancelled, Is.False,
                "Mend Bones must remain available when bone integrity is low even after trauma entities are gone.");
        });
    }
}
