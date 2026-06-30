using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server.Backmen.Surgery.Consciousness.Systems;
using Content.Shared.Backmen.Body.Systems;
using Content.Shared.Backmen.CCVar;
using Content.Shared.Backmen.Surgery;
using Content.Shared.Medical.Surgery.Conditions;
using Content.Shared.Body.Part;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Robust.Server.Player;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Backmen.Surgery;

[TestFixture]
[EnsureCVar(Side.Server, typeof(CCVars), nameof(CCVars.PainEnabled), true)]
public sealed class StarvingPainSurgeryTest : GameTest
{
    private static readonly EntProtoId MobHuman = "MobHuman";
    private static readonly EntProtoId RelieveSurgery = "SurgeryRelieveStarvingPain";
    private static readonly EntProtoId RelieveStep = "SurgeryStepRelieveStarvingPain";

    public override PoolSettings PoolSettings => new()
    {
        Connected = true,
        Dirty = true,
    };

    private void AttachActor(EntityUid uid)
    {
        Server.PlayerMan.SetAttachedEntity(Pair.Player!, uid);
    }

    [Test]
    public async Task RelieveStarvingPain_RemovesPainButKeepsHunger()
    {
        var map = await Pair.CreateTestMap();
        var hungerSys = Server.EntMan.System<HungerSystem>();
        var consciousnessSys = Server.EntMan.System<ServerConsciousnessSystem>();
        var bodySys = Server.EntMan.System<BkmBodySharedSystem>();
        EntityUid patient = default;
        EntityUid surgery = default;
        EntityUid step = default;

        await Server.WaitPost(() =>
        {
            patient = Server.EntMan.SpawnAtPosition(MobHuman, map.GridCoords);
            AttachActor(patient);
            hungerSys.ConfigureStarvingPain(patient, growthRate: 5f);
            var hunger = Server.EntMan.GetComponent<HungerComponent>(patient);
            hungerSys.SetHunger(patient, hunger.Thresholds[HungerThreshold.Starving] - 1, hunger);
        });

        await Pair.RunTicksSync(90);

        await Server.WaitPost(() =>
        {
            Assert.That(bodySys.TryGetWoundableTargetByType(patient, BodyPartType.Chest, null, out var chest), Is.True);
            Assert.That(consciousnessSys.GetPainCauses(patient)?.ContainsKey("Starving"), Is.True);

            surgery = Server.EntMan.Spawn(RelieveSurgery);
            var validEv = new SurgeryValidEvent(patient, chest);
            Server.EntMan.EventBus.RaiseLocalEvent(surgery, ref validEv);
            Assert.That(validEv.Cancelled, Is.False, "Surgery should be valid while starving pain is present.");

            step = Server.EntMan.Spawn(RelieveStep);
            var stepEv = new SurgeryStepEvent(patient, patient, chest, [], surgery);
            Server.EntMan.EventBus.RaiseLocalEvent(step, ref stepEv);
        });

        await Server.WaitAssertion(() =>
        {
            var causes = consciousnessSys.GetPainCauses(patient);
            Assert.That(causes == null || !causes.ContainsKey("Starving"), Is.True);

            var hunger = Server.EntMan.GetComponent<HungerComponent>(patient);
            Assert.That(hunger.CurrentThreshold, Is.LessThanOrEqualTo(HungerThreshold.Starving));
        });
    }
}
