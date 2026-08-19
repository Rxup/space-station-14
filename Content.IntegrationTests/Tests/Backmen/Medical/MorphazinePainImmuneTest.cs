using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server.Backmen.Surgery.Pain.Systems;
using Content.Server.Body.Systems;
using Content.Shared.Backmen.CCVar;
using Content.Shared.Backmen.Surgery.Pain.Components;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Content.Shared.StatusEffectNew;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Backmen.Medical;

[TestFixture]
[EnsureCVar(Side.Server, typeof(CCVars), nameof(CCVars.PainEnabled), true)]
public sealed class MorphazinePainImmuneTest : GameTest
{
    private static readonly EntProtoId MobHuman = "MobHuman";
    private static readonly EntProtoId PainImmuneEffect = "StatusEffectPainImmune";
    private static readonly ProtoId<ReagentPrototype> MorphazineReagent = "Morphazine";

    public override PoolSettings PoolSettings => new()
    {
        Connected = false,
        Dirty = true,
    };

    [Test]
    public async Task StatusEffect_GrantsPainImmuneWithoutComponentOnMob()
    {
        var map = await Pair.CreateTestMap();
        EntityUid human = default;

        await Server.WaitPost(() =>
        {
            human = Server.EntMan.SpawnAtPosition(MobHuman, map.GridCoords);
            var statusSys = Server.EntMan.System<StatusEffectsSystem>();
            Assert.That(
                statusSys.TryUpdateStatusEffectDuration(human, PainImmuneEffect, TimeSpan.FromSeconds(30)),
                Is.True,
                "StatusEffectPainImmune should apply to a human.");
        });

        await Pair.RunTicksSync(5);

        await Server.WaitAssertion(() =>
        {
            var painSys = Server.EntMan.System<ServerPainSystem>();
            Assert.That(
                Server.EntMan.HasComponent<PainImmuneComponent>(human),
                Is.False,
                "PainImmune must stay on the status effect, not the mob.");
            Assert.That(painSys.IsPainImmune(human), Is.True, "Helper should see PainImmune on the status effect.");
        });

        await Server.WaitPost(() =>
        {
            var statusSys = Server.EntMan.System<StatusEffectsSystem>();
            Assert.That(statusSys.TryRemoveStatusEffect(human, PainImmuneEffect), Is.True);
        });

        await Pair.RunTicksSync(5);

        await Server.WaitAssertion(() =>
        {
            var painSys = Server.EntMan.System<ServerPainSystem>();
            Assert.That(painSys.IsPainImmune(human), Is.False, "Helper should be false after the status effect is removed.");
        });
    }

    [Test]
    public async Task Morphazine_AppliesPainImmuneStatusEffect()
    {
        var map = await Pair.CreateTestMap();
        var bloodstreamSys = Server.EntMan.System<BloodstreamSystem>();
        EntityUid human = default;

        await Server.WaitPost(() =>
        {
            human = Server.EntMan.SpawnAtPosition(MobHuman, map.GridCoords);
            Assert.That(Server.EntMan.TryGetComponent(human, out BloodstreamComponent? stream), Is.True);
            var solution = new Solution();
            // Stay above the crash threshold (max 2u) so the test doesn't immediately sleep.
            solution.AddReagent(MorphazineReagent, FixedPoint2.New(10));
            Assert.That(bloodstreamSys.TryAddToBloodstream((human, stream), solution), Is.True);
        });

        await Pair.RunTicksSync(90);

        await Server.WaitAssertion(() =>
        {
            var painSys = Server.EntMan.System<ServerPainSystem>();
            var statusSys = Server.EntMan.System<StatusEffectsSystem>();
            Assert.That(
                Server.EntMan.HasComponent<PainImmuneComponent>(human),
                Is.False,
                "Morphazine must not copy PainImmune onto the mob.");
            Assert.That(
                statusSys.HasStatusEffect(human, PainImmuneEffect),
                Is.True,
                "Morphazine should apply StatusEffectPainImmune while metabolizing.");
            Assert.That(painSys.IsPainImmune(human), Is.True, "Morphazine should make IsPainImmune true via the status effect.");
        });
    }
}
