using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server.Backmen.Surgery.Wounds.Systems;
using Content.Server.Backmen.Targeting;
using Content.Shared.Backmen.Surgery.Wounds;
using Content.Shared.Backmen.Targeting;
using Content.Shared.Body;
using Content.Shared.Body.Organ;
using Content.Shared.Hands.Components;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.Backmen.Body;

/// <summary>
/// Detaching an arm must not leave its hand orphaned on the patient body.
/// </summary>
[TestFixture]
public sealed class ArmHandDetachSyncTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [Test]
    public async Task RemovingArm_RemovesHandFromPatient_AndSyncsStatus()
    {
        var map = await Pair.CreateTestMap();
        NetEntity netPatient = default;

        await Server.WaitAssertion(() =>
        {
            var patient = Server.EntMan.SpawnEntity("MobHuman", map.MapCoords);
            var bodySys = Server.EntMan.System<BodySystem>();
            var bkmBodySys = Server.EntMan.System<Content.Shared.Backmen.Body.Systems.BkmBodySharedSystem>();

            Assert.That(bodySys.TryGetOrganByCategory(patient, "ArmLeft", out var arm), Is.True);
            Assert.That(bodySys.TryGetOrganByCategory(patient, "HandLeft", out _), Is.True);

            Assert.That(bkmBodySys.RemoveOrgan(arm, Server.EntMan.GetComponent<OrganComponent>(arm)), Is.True);

            netPatient = Server.EntMan.GetNetEntity(patient);
        });

        await Server.WaitIdleAsync();

        await Server.WaitAssertion(() =>
        {
            var patient = Server.EntMan.GetEntity(netPatient);
            var bodySys = Server.EntMan.System<BodySystem>();
            var woundSys = Server.EntMan.System<ServerWoundSystem>();
            var targetingSys = Server.EntMan.System<TargetingSystem>();

            Assert.That(bodySys.TryGetOrganByCategory(patient, "ArmLeft", out _), Is.False,
                "Arm should no longer be on the patient.");
            Assert.That(bodySys.TryGetOrganByCategory(patient, "HandLeft", out _), Is.False,
                "Hand should not remain orphaned on the patient after arm removal.");

            var states = woundSys.GetWoundableStatesOnBody(patient);
            Assert.That(states[TargetBodyPart.LeftArm], Is.EqualTo(WoundableSeverity.Loss));
            Assert.That(states[TargetBodyPart.LeftHand], Is.EqualTo(WoundableSeverity.Loss));

            var surgeryTargets = targetingSys.GetSurgeryTargets(patient).ToList();
            Assert.That(surgeryTargets.Any(uid =>
                Server.EntMan.TryGetComponent(uid, out OrganComponent? organ) && organ.Category == "HandLeft"), Is.False,
                "Hand surgery should not be offered without its arm.");

            if (Server.EntMan.TryGetComponent(patient, out HandsComponent? hands))
                Assert.That(hands.Count, Is.EqualTo(1), "Patient should have one hand slot after losing the left arm.");
        });
    }
}
