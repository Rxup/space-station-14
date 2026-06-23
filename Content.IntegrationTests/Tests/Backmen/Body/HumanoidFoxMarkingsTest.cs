using Content.IntegrationTests.Fixtures;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Backmen.Body;

[TestFixture]
public sealed class HumanoidFoxMarkingsTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [Test]
    public async Task HumanoidFoxes_TorsoSupportsTailMarkings()
    {
        await Server.WaitAssertion(() =>
        {
            var markingManager = Server.ResolveDependency<MarkingManager>();
            var data = markingManager.GetMarkingData("HumanoidFoxes");

            Assert.That(data.TryGetValue("Torso", out var torsoData), Is.True);
            Assert.That(torsoData.Layers, Does.Contain(HumanoidVisualLayers.Tail));

            var tailMarkings = markingManager.MarkingsByLayerAndGroupAndSex(
                HumanoidVisualLayers.Tail,
                torsoData.Group,
                Sex.Male);

            Assert.That(tailMarkings.Count, Is.GreaterThan(0),
                "Fox species should expose at least one selectable tail marking.");

            Assert.That(tailMarkings.ContainsKey("FoxTail"), Is.True);
            Assert.That(tailMarkings.ContainsKey("WolfTail"), Is.True);
            Assert.That(tailMarkings.ContainsKey("TailSnakeAnimated"), Is.False,
                "Unrelated tail markings without fox whitelist should not appear for HumanoidFoxes.");
        });
    }
}
