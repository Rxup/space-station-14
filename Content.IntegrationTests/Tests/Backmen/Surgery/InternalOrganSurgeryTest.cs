using Content.IntegrationTests.Fixtures;
using Content.Shared.Backmen.Body.Systems;
using Content.Shared.Backmen.Surgery.Body.Organs;
using Content.Shared.Body;
using Content.Shared.Body.Part;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.Backmen.Surgery;

/// <summary>
/// Internal organ surgeries (heart, lungs, etc.) must resolve only on the correct external host part.
/// </summary>
[TestFixture]
public sealed class InternalOrganSurgeryTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [Test]
    public async Task InternalOrgans_ScopedToHostPart()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var patient = Server.EntMan.SpawnEntity("MobHuman", map.MapCoords);
            var bodySys = Server.EntMan.System<BkmBodySharedSystem>();

            Assert.That(
                bodySys.TryGetWoundableTargetByType(patient, BodyPartType.Chest, null, out var torso),
                Is.True,
                "Human should have a torso woundable target");

            Assert.That(
                bodySys.TryGetWoundableTargetByType(patient, BodyPartType.Head, null, out var head),
                Is.True,
                "Human should have a head woundable target");

            Assert.That(
                bodySys.TryGetInternalOrgansForHostPart(patient, torso, typeof(HeartComponent), out var heartOnTorso),
                Is.True);
            Assert.That(heartOnTorso, Has.Count.EqualTo(1));

            Assert.That(
                bodySys.TryGetInternalOrgansForHostPart(patient, torso, typeof(LungComponent), out var lungsOnTorso),
                Is.True);
            Assert.That(lungsOnTorso, Has.Count.EqualTo(1));

            Assert.That(
                bodySys.TryGetInternalOrgansForHostPart(patient, torso, typeof(LiverComponent), out var liverOnTorso),
                Is.True);
            Assert.That(liverOnTorso, Has.Count.EqualTo(1));

            Assert.That(
                bodySys.TryGetInternalOrgansForHostPart(patient, head, typeof(BrainComponent), out var brainOnHead),
                Is.True);
            Assert.That(brainOnHead, Has.Count.EqualTo(1));

            Assert.That(
                bodySys.TryGetInternalOrgansForHostPart(patient, head, typeof(HeartComponent), out _),
                Is.False,
                "Heart should not resolve when operating on the head");

            Assert.That(
                bodySys.TryGetInternalOrgansForHostPart(patient, torso, typeof(BrainComponent), out _),
                Is.False,
                "Brain should not resolve when operating on the torso");
        });
    }
}
