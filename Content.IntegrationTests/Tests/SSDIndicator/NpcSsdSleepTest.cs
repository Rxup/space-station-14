using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Shared.CCVar;
using Content.Shared.NPC;
using Content.Shared.SSDIndicator;
using Content.Shared.StatusEffectNew;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.SSDIndicator;

/// <summary>
/// Active AI NPCs must not receive SSD forced sleep (which includes stun/knockdown).
/// </summary>
[TestFixture]
[EnsureCVar(Side.Server, typeof(CCVars), nameof(CCVars.ICSSDSleep), true)]
public sealed class NpcSsdSleepTest : GameTest
{
    private static readonly EntProtoId MobCarp = "MobCarp";

    public override PoolSettings PoolSettings => new()
    {
        Connected = true,
        Dirty = true,
    };

    [Test]
    public async Task ActiveNpc_DoesNotReceiveSsdSleep()
    {
        await OverrideCVar(Side.Server, CCVars.ICSSDSleepTime, 1f);

        var map = await Pair.CreateTestMap();
        EntityUid carp = default;

        await Server.WaitPost(() =>
        {
            carp = Server.EntMan.SpawnAtPosition(MobCarp, map.GridCoords);
        });

        await Pair.RunTicksSync(150);

        await Server.WaitAssertion(() =>
        {
            var statusSys = Server.EntMan.System<StatusEffectsSystem>();

            Assert.That(Server.EntMan.HasComponent<ActiveNPCComponent>(carp), Is.True,
                "Carp should be an active AI NPC after map init.");

            Assert.That(statusSys.HasStatusEffect(carp, SSDIndicatorSystem.StatusEffectSSDSleeping), Is.False,
                "Active NPCs must not receive SSD forced sleep.");

            if (Server.EntMan.TryGetComponent(carp, out SSDIndicatorComponent? ssd))
                Assert.That(ssd.IsSSD, Is.False, "Active NPCs with SSD tracking must not be marked as SSD.");
        });
    }
}
