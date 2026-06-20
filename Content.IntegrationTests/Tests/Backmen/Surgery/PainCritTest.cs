using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server.Backmen.Surgery.Pain.Systems;
using Content.Server.Backmen.Standing;
using Content.Shared.Backmen.CCVar;
using Content.Shared.Backmen.Surgery.Consciousness.Components;
using Content.Shared.Backmen.Surgery.Pain.Components;
using Content.Shared.Backmen.Standing;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Standing;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Backmen.Surgery;

/// <summary>
/// Pain crit (SoftCritical) must persist while pain is high and must not oscillate with Alive.
/// </summary>
[TestFixture]
[EnsureCVar(Side.Server, typeof(CCVars), nameof(CCVars.PainEnabled), true)]
public sealed class PainCritTest : GameTest
{
    private static readonly EntProtoId MobHuman = "MobHuman";

    public override PoolSettings PoolSettings => new()
    {
        Connected = true,
        Dirty = true,
    };

    [Test]
    public async Task ForcePainCrit_EntersSoftCriticalAndStaysDown()
    {
        var map = await Pair.CreateTestMap();
        var painSys = Server.EntMan.System<ServerPainSystem>();
        EntityUid human = default;

        await Server.WaitPost(() =>
        {
            human = Server.EntMan.SpawnAtPosition(MobHuman, map.GridCoords);
            Assert.That(Server.EntMan.TryGetComponent(human, out ConsciousnessComponent? consciousness), Is.True);
            Assert.That(consciousness!.NerveSystem, Is.Not.Null);

            painSys.ForcePainCrit(consciousness.NerveSystem.Value, TimeSpan.FromSeconds(30));
        });

        await Pair.RunTicksSync(15);

        await Server.WaitAssertion(() =>
        {
            Assert.That(Server.EntMan.TryGetComponent(human, out MobStateComponent? mobState), Is.True);
            Assert.That(mobState!.CurrentState, Is.EqualTo(MobState.SoftCritical),
                "Forced pain crit should put the mob in SoftCritical.");

            Assert.That(Server.EntMan.TryGetComponent(human, out StandingStateComponent? standing), Is.True);
            Assert.That(standing!.Standing, Is.False, "Pain crit should keep the mob down.");
        });
    }

    [Test]
    public async Task ForcePainCrit_DoesNotOscillateWithAlive()
    {
        var map = await Pair.CreateTestMap();
        var painSys = Server.EntMan.System<ServerPainSystem>();
        EntityUid human = default;
        var stateChanges = 0;
        MobState? lastState = null;

        await Server.WaitPost(() =>
        {
            human = Server.EntMan.SpawnAtPosition(MobHuman, map.GridCoords);
            var consciousness = Server.EntMan.GetComponent<ConsciousnessComponent>(human);
            painSys.ForcePainCrit(consciousness.NerveSystem!.Value, TimeSpan.FromSeconds(30));
        });

        for (var i = 0; i < 20; i++)
        {
            await Pair.RunTicksSync(1);

            await Server.WaitAssertion(() =>
            {
                var mobState = Server.EntMan.GetComponent<MobStateComponent>(human).CurrentState;
                if (lastState != mobState)
                {
                    stateChanges++;
                    lastState = mobState;
                }
            });
        }

        await Server.WaitAssertion(() =>
        {
            var mobState = Server.EntMan.GetComponent<MobStateComponent>(human);
            Assert.That(mobState.CurrentState, Is.EqualTo(MobState.SoftCritical),
                "Pain crit should settle in SoftCritical, not bounce back to Alive.");

            Assert.That(stateChanges, Is.LessThanOrEqualTo(2),
                "Pain crit should not repeatedly toggle mob state (fall/stand loop).");
        });
    }

    [Test]
    public async Task SoftPainCap_EntersSoftCriticalWithoutForcePainCrit()
    {
        var map = await Pair.CreateTestMap();
        var painSys = Server.EntMan.System<ServerPainSystem>();
        EntityUid human = default;

        await Server.WaitPost(() =>
        {
            human = Server.EntMan.SpawnAtPosition(MobHuman, map.GridCoords);
            Assert.That(Server.EntMan.TryGetComponent(human, out ConsciousnessComponent? consciousness), Is.True);
            Assert.That(consciousness!.NerveSystem, Is.Not.Null);

            var nerveSysEnt = consciousness.NerveSystem.Value;
            var nerveSys = Server.EntMan.GetComponent<NerveSystemComponent>(nerveSysEnt);
            Assert.That(
                painSys.TryAddPainModifier(nerveSysEnt, human, "PainCritTest", nerveSys.SoftPainCap),
                Is.True,
                "Pain at SoftPainCap should be applied.");
        });

        await Pair.RunTicksSync(5);

        await Server.WaitAssertion(() =>
        {
            Assert.That(Server.EntMan.TryGetComponent(human, out MobStateComponent? mobState), Is.True);
            Assert.That(mobState!.CurrentState, Is.EqualTo(MobState.SoftCritical),
                "Pain at SoftPainCap should enter SoftCritical, not regular Critical.");

            Assert.That(Server.EntMan.TryGetComponent(human, out StandingStateComponent? standing), Is.True);
            Assert.That(standing!.Standing, Is.False);
        });
    }

    [Test]
    public async Task ForcePainCrit_CancelsStandUpAttempt()
    {
        var map = await Pair.CreateTestMap();
        var painSys = Server.EntMan.System<ServerPainSystem>();
        var layingDownSys = Server.EntMan.System<LayingDownSystem>();
        var standingSys = Server.EntMan.System<StandingStateSystem>();
        EntityUid human = default;

        await Server.WaitPost(() =>
        {
            human = Server.EntMan.SpawnAtPosition(MobHuman, map.GridCoords);
            Assert.That(standingSys.Down(human), Is.True);

            var standing = Server.EntMan.GetComponent<StandingStateComponent>(human);
            Assert.That(standing.CurrentState, Is.EqualTo(StandingState.Lying));

            Assert.That(layingDownSys.TryStandUp(human), Is.True);
            standing = Server.EntMan.GetComponent<StandingStateComponent>(human);
            Assert.That(standing.CurrentState, Is.EqualTo(StandingState.GettingUp),
                "Stand-up DoAfter should begin while still alive.");

            var consciousness = Server.EntMan.GetComponent<ConsciousnessComponent>(human);
            painSys.ForcePainCrit(consciousness.NerveSystem!.Value, TimeSpan.FromSeconds(30));
        });

        await Pair.RunTicksSync(30);

        await Server.WaitAssertion(() =>
        {
            var mobState = Server.EntMan.GetComponent<MobStateComponent>(human);
            Assert.That(mobState.CurrentState, Is.EqualTo(MobState.SoftCritical));

            var standing = Server.EntMan.GetComponent<StandingStateComponent>(human);
            Assert.That(standing.Standing, Is.False);
            Assert.That(standing.CurrentState, Is.EqualTo(StandingState.Lying),
                "Pain crit should cancel stand-up and keep the mob lying down.");
        });
    }
}
