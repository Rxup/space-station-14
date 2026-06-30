using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server.Backmen.Surgery.Consciousness.Systems;
using Content.Shared.Backmen.CCVar;
using Content.Shared.Backmen.Nutrition;
using Content.Shared.Backmen.Surgery.Wounds.Systems;
using Content.Shared.Body;
using Content.Shared.Mobs.Systems;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Server.Player;

namespace Content.IntegrationTests.Tests.Backmen.Nutrition;

[TestFixture]
[EnsureCVar(Side.Server, typeof(CCVars), nameof(CCVars.PainEnabled), true)]
public sealed class HungerPainTest : GameTest
{
    private static readonly EntProtoId MobHuman = "MobHuman";
    private const float ExpectedBaseDecayRate = 0.04166666665f;

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
    public async Task Starving_AppliesStarvingPainCause()
    {
        var map = await Pair.CreateTestMap();
        var hungerSys = Server.EntMan.System<HungerSystem>();
        var consciousnessSys = Server.EntMan.System<ServerConsciousnessSystem>();
        EntityUid human = default;

        await Server.WaitPost(() =>
        {
            human = Server.EntMan.SpawnAtPosition(MobHuman, map.GridCoords);
            AttachActor(human);
            hungerSys.ConfigureStarvingPain(human, growthRate: 5f);
            var hunger = Server.EntMan.GetComponent<HungerComponent>(human);
            hungerSys.SetHunger(human, hunger.Thresholds[HungerThreshold.Starving] - 1, hunger);
        });

        await Pair.RunTicksSync(90);

        await Server.WaitAssertion(() =>
        {
            var causes = consciousnessSys.GetPainCauses(human);
            Assert.That(causes, Is.Not.Null);
            Assert.That(causes!.ContainsKey("Starving"), Is.True);
            Assert.That(causes.ContainsKey("WoundPain"), Is.False, "Starving pain must not be shown as wound pain.");
        });
    }

    [Test]
    public async Task Starving_DoesNotCreateColdWounds()
    {
        var map = await Pair.CreateTestMap();
        var hungerSys = Server.EntMan.System<HungerSystem>();
        var woundSys = Server.EntMan.System<WoundSystem>();
        var consciousnessSys = Server.EntMan.System<ServerConsciousnessSystem>();
        EntityUid human = default;
        var coldWoundsBefore = 0;

        await Server.WaitPost(() =>
        {
            human = Server.EntMan.SpawnAtPosition(MobHuman, map.GridCoords);
            AttachActor(human);
            var body = Server.EntMan.GetComponent<BodyComponent>(human);
            coldWoundsBefore = woundSys.GetBodyWounds(human, body)
                .Count(wound => wound.Comp.DamageType == "Cold");

            Assert.That(Server.EntMan.GetComponent<HungerComponent>(human).StarvationDamage, Is.Null);

            var hunger = Server.EntMan.GetComponent<HungerComponent>(human);
            hungerSys.SetHunger(human, hunger.Thresholds[HungerThreshold.Starving] - 1, hunger);
        });

        await Pair.RunTicksSync(90);

        await Server.WaitAssertion(() =>
        {
            var body = Server.EntMan.GetComponent<BodyComponent>(human);
            var coldWoundsAfter = woundSys.GetBodyWounds(human, body)
                .Count(wound => wound.Comp.DamageType == "Cold");

            Assert.That(
                coldWoundsAfter,
                Is.EqualTo(coldWoundsBefore),
                "Starving should not create new Cold wounds.");

            var causes = consciousnessSys.GetPainCauses(human);
            Assert.That(causes, Is.Not.Null);
            Assert.That(causes!.ContainsKey("Starving"), Is.True, "Starving should apply pain instead of Cold wounds.");
            Assert.That(causes.ContainsKey("WoundPain"), Is.False, "Starving pain must not be shown as wound pain.");
        });
    }

    [Test]
    public async Task Eating_ReducesStarvingPain()
    {
        var map = await Pair.CreateTestMap();
        var hungerSys = Server.EntMan.System<HungerSystem>();
        var consciousnessSys = Server.EntMan.System<ServerConsciousnessSystem>();
        EntityUid human = default;
        float painBeforeEat = 0;

        await Server.WaitPost(() =>
        {
            human = Server.EntMan.SpawnAtPosition(MobHuman, map.GridCoords);
            AttachActor(human);
            hungerSys.ConfigureStarvingPain(human, growthRate: 3f, decayRate: 6f);
            var hunger = Server.EntMan.GetComponent<HungerComponent>(human);
            hungerSys.SetHunger(human, hunger.Thresholds[HungerThreshold.Starving] - 5, hunger);
        });

        await Pair.RunTicksSync(120);

        await Server.WaitAssertion(() =>
        {
            painBeforeEat = consciousnessSys.GetPainCauses(human)?["Starving"] ?? 0;
            Assert.That(painBeforeEat, Is.GreaterThan(0));
        });

        await Server.WaitPost(() =>
        {
            var hunger = Server.EntMan.GetComponent<HungerComponent>(human);
            hungerSys.SetHunger(human, hunger.Thresholds[HungerThreshold.Okay], hunger);
        });

        await Pair.RunTicksSync(90);

        await Server.WaitAssertion(() =>
        {
            var causes = consciousnessSys.GetPainCauses(human);
            var painAfter = causes != null && causes.TryGetValue("Starving", out var starving) ? starving : 0;
            Assert.That(painAfter, Is.LessThan(painBeforeEat));
        });
    }

    [Test]
    public async Task Starving_WithoutActor_DoesNotApplyPain()
    {
        var map = await Pair.CreateTestMap();
        var hungerSys = Server.EntMan.System<HungerSystem>();
        var consciousnessSys = Server.EntMan.System<ServerConsciousnessSystem>();
        EntityUid human = default;

        await Server.WaitPost(() =>
        {
            human = Server.EntMan.SpawnAtPosition(MobHuman, map.GridCoords);
            hungerSys.ConfigureStarvingPain(human, growthRate: 5f);
            var hunger = Server.EntMan.GetComponent<HungerComponent>(human);
            hungerSys.SetHunger(human, hunger.Thresholds[HungerThreshold.Starving] - 1, hunger);
        });

        await Pair.RunTicksSync(90);

        await Server.WaitAssertion(() =>
        {
            var causes = consciousnessSys.GetPainCauses(human);
            Assert.That(causes == null || !causes.ContainsKey("Starving"), Is.True);
            Assert.That(Server.EntMan.HasComponent<HungerPainTrackerComponent>(human), Is.False);
        });
    }

    [Test]
    public async Task MobHuman_HasFortyFiveMinuteHungerDecayRate()
    {
        var map = await Pair.CreateTestMap();
        EntityUid human = default;

        await Server.WaitPost(() => human = Server.EntMan.SpawnAtPosition(MobHuman, map.GridCoords));

        await Server.WaitAssertion(() =>
        {
            var hunger = Server.EntMan.GetComponent<HungerComponent>(human);
            Assert.That(hunger.BaseDecayRate, Is.EqualTo(ExpectedBaseDecayRate).Within(0.0001f));
            Assert.That(hunger.StarvationDamage, Is.Null);
        });
    }

    [Explicit("Slow balance check (~3–4 min). Run manually: dotnet test --filter Starving_KillsWithinThreeToFourMinutes")]
    [Test]
    public async Task Starving_KillsWithinThreeToFourMinutes()
    {
        var map = await Pair.CreateTestMap();
        var hungerSys = Server.EntMan.System<HungerSystem>();
        var mobStateSys = Server.EntMan.System<MobStateSystem>();
        EntityUid human = default;

        await Server.WaitPost(() =>
        {
            human = Server.EntMan.SpawnAtPosition(MobHuman, map.GridCoords);
            AttachActor(human);
            var hunger = Server.EntMan.GetComponent<HungerComponent>(human);
            hungerSys.SetHunger(human, 5, hunger);
        });

        // ~3.5 minutes at 30 tps (6300 ticks ≈ 210 s).
        await Pair.RunTicksSync(6300);

        await Server.WaitAssertion(() =>
        {
            Assert.That(
                mobStateSys.IsDead(human) || mobStateSys.IsCritical(human),
                Is.True,
                "Starving pain should incapacitate or kill within ~3–4 minutes.");
        });
    }
}
