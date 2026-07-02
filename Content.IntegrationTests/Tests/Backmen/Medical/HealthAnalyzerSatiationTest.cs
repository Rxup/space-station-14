using Content.IntegrationTests.Fixtures;
using Content.Server.Medical;
using Content.Shared.MedicalScanner;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Backmen.Medical;

[TestFixture]
public sealed class HealthAnalyzerSatiationTest : GameTest
{
    private static readonly EntProtoId MobHuman = "MobHuman";

    public override PoolSettings PoolSettings => new()
    {
        Connected = true,
        Dirty = true,
    };

    [Test]
    public async Task AnalyzerReportsHungerAndThirstAlerts()
    {
        var map = await Pair.CreateTestMap();
        var analyzerSys = Server.EntMan.System<HealthAnalyzerSystem>();
        var hungerSys = Server.EntMan.System<HungerSystem>();
        var thirstSys = Server.EntMan.System<ThirstSystem>();
        EntityUid human = default;

        await Server.WaitPost(() =>
        {
            human = Server.EntMan.SpawnAtPosition(MobHuman, map.GridCoords);

            var hunger = Server.EntMan.GetComponent<HungerComponent>(human);
            hungerSys.SetHunger(human, hunger.Thresholds[HungerThreshold.Starving] - 1, hunger);

            var thirst = Server.EntMan.GetComponent<ThirstComponent>(human);
            thirstSys.SetThirst(human, thirst, thirst.ThirstThresholds[ThirstThreshold.Parched] - 1);
        });

        await Server.WaitAssertion(() =>
        {
            var hunger = Server.EntMan.GetComponent<HungerComponent>(human);
            var thirst = Server.EntMan.GetComponent<ThirstComponent>(human);
            var state = analyzerSys.GetHealthAnalyzerUiState(human);
            Assert.That(state.HungerAlert, Is.EqualTo(HungerThreshold.Starving));
            Assert.That(state.ThirstAlert, Is.EqualTo(ThirstThreshold.Parched));
            Assert.That(state.HungerLevel, Is.EqualTo(hungerSys.GetHungerLevel(hunger)).Within(0.01f));
            Assert.That(state.ThirstLevel, Is.EqualTo(thirstSys.GetThirstLevel(thirst)).Within(0.01f));
        });

        await Server.WaitPost(() =>
        {
            var hunger = Server.EntMan.GetComponent<HungerComponent>(human);
            hungerSys.SetHunger(human, hunger.Thresholds[HungerThreshold.Okay], hunger);

            var thirst = Server.EntMan.GetComponent<ThirstComponent>(human);
            thirstSys.SetThirst(human, thirst, thirst.ThirstThresholds[ThirstThreshold.Okay]);
        });

        await Server.WaitAssertion(() =>
        {
            var hunger = Server.EntMan.GetComponent<HungerComponent>(human);
            var thirst = Server.EntMan.GetComponent<ThirstComponent>(human);
            var state = analyzerSys.GetHealthAnalyzerUiState(human);
            Assert.That(state.HungerAlert, Is.Null);
            Assert.That(state.ThirstAlert, Is.Null);
            Assert.That(state.HungerLevel, Is.EqualTo(hungerSys.GetHungerLevel(hunger)).Within(0.01f));
            Assert.That(state.ThirstLevel, Is.EqualTo(thirstSys.GetThirstLevel(thirst)).Within(0.01f));
            Assert.That(state.HungerLevel, Is.GreaterThanOrEqualTo(0.99f));
            Assert.That(state.ThirstLevel, Is.GreaterThanOrEqualTo(0.99f));
        });
    }
}
