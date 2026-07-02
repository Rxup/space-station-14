using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server.Backmen.Surgery.Pain.Systems;
using Content.Server.Body.Systems;
using Content.Shared.Backmen.Body.Systems;
using Content.Shared.Backmen.CCVar;
using Content.Shared.Backmen.Surgery.Consciousness.Components;
using Content.Shared.Backmen.Surgery.Pain.Components;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Damage.Components;
using Content.Shared.StatusEffectNew;
using Content.Shared.Backmen.Targeting;
using Content.Shared.FixedPoint;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Backmen.Medical;

[TestFixture]
[EnsureCVar(Side.Server, typeof(CCVars), nameof(CCVars.PainEnabled), true)]
public sealed class MorphinePainTest : GameTest
{
    private static readonly EntProtoId MobHuman = "MobHuman";
    private static readonly ProtoId<ReagentPrototype> MorphineReagent = "Morphine";
    private const string PainSuppressantId = "PainSuppressant";

    public override PoolSettings PoolSettings => new()
    {
        // Chemistry/pain metabolism is server-authoritative; avoid client teardown flakes from sleeping/snore comps.
        Connected = false,
        Dirty = true,
    };

    [Test]
    public async Task Morphine_SuppressesPainAfterMetabolism()
    {
        var map = await Pair.CreateTestMap();
        var damageSys = Server.EntMan.System<DamageableSystem>();
        var bloodstreamSys = Server.EntMan.System<BloodstreamSystem>();
        var painSys = Server.EntMan.System<ServerPainSystem>();
        var bodySys = Server.EntMan.System<BkmBodySharedSystem>();
        EntityUid human = default;
        float painBeforeMorphine = 0;

        await Server.WaitPost(() =>
        {
            human = Server.EntMan.SpawnAtPosition(MobHuman, map.GridCoords);

            Assert.That(Server.EntMan.TryGetComponent(human, out ConsciousnessComponent? consciousness), Is.True);
            Assert.That(consciousness!.NerveSystem, Is.Not.Null, "Human must have a nerve system for morphine pain suppression.");

            var damage = new DamageSpecifier { DamageDict = { ["Blunt"] = FixedPoint2.New(30) } };
            Assert.That(
                damageSys.ChangeDamage(human, damage, targetPart: TargetBodyPart.Head).GetTotal() > 0,
                Is.True,
                "Human should take wound damage before morphine.");

            painBeforeMorphine = (float) Server.EntMan.GetComponent<NerveSystemComponent>(consciousness.NerveSystem!.Value).Pain;
            Assert.That(painBeforeMorphine, Is.GreaterThan(0), "Damaged human should be in pain before morphine.");

            Assert.That(Server.EntMan.TryGetComponent(human, out BloodstreamComponent? stream), Is.True);
            var morphine = new Solution();
            // 10u: SuppressPain + IgnoreSlowOnDamage, but below drowsiness threshold (12u).
            morphine.AddReagent(MorphineReagent, FixedPoint2.New(10));
            Assert.That(
                bloodstreamSys.TryAddToBloodstream((human, stream), morphine),
                Is.True,
                "Morphine must enter the bloodstream.");
        });

        await Pair.RunTicksSync(90);

        await Server.WaitAssertion(() =>
        {
            var consciousness = Server.EntMan.GetComponent<ConsciousnessComponent>(human);
            var nerveSys = Server.EntMan.GetComponent<NerveSystemComponent>(consciousness.NerveSystem!.Value);
            var painAfterMorphine = (float) nerveSys.Pain;

            Assert.That(
                bodySys.TryGetWoundableTargetByType(human, BodyPartType.Head, null, out var head),
                Is.True,
                "Morphine SuppressPain requires a head woundable target.");

            Assert.That(
                painSys.TryGetPainModifier(nerveSys.Owner, head, PainSuppressantId, out var suppressant),
                Is.True,
                "Morphine should apply a pain suppressant modifier while metabolizing.");

            Assert.That(suppressant, Is.Not.Null);
            Assert.That(suppressant!.Value.Change, Is.LessThan(FixedPoint2.Zero),
                "Pain suppressant modifier should reduce pain.");

            Assert.That(painAfterMorphine, Is.LessThan(painBeforeMorphine),
                "Morphine should lower total pain after metabolism ticks.");
        });
    }

    [Test]
    public async Task Morphine_GrantsIgnoreSlowOnDamage()
    {
        var map = await Pair.CreateTestMap();
        var bloodstreamSys = Server.EntMan.System<BloodstreamSystem>();
        EntityUid human = default;

        await Server.WaitPost(() =>
        {
            human = Server.EntMan.SpawnAtPosition(MobHuman, map.GridCoords);

            Assert.That(Server.EntMan.TryGetComponent(human, out BloodstreamComponent? stream), Is.True);
            var morphine = new Solution();
            morphine.AddReagent(MorphineReagent, FixedPoint2.New(10));
            Assert.That(bloodstreamSys.TryAddToBloodstream((human, stream), morphine), Is.True);
        });

        await Pair.RunTicksSync(90);

        await Server.WaitAssertion(() =>
        {
            Assert.That(
                Server.EntMan.HasComponent<IgnoreSlowOnDamageComponent>(human),
                Is.True,
                "Morphine should grant IgnoreSlowOnDamage at >=10u in bloodstream.");
        });
    }

    [Test]
    public async Task Morphine_AppliesDrowsinessAtHighDose()
    {
        var map = await Pair.CreateTestMap();
        var bloodstreamSys = Server.EntMan.System<BloodstreamSystem>();
        EntityUid human = default;

        await Server.WaitPost(() =>
        {
            human = Server.EntMan.SpawnAtPosition(MobHuman, map.GridCoords);

            Assert.That(Server.EntMan.TryGetComponent(human, out BloodstreamComponent? stream), Is.True);
            var morphine = new Solution();
            morphine.AddReagent(MorphineReagent, FixedPoint2.New(15));
            Assert.That(bloodstreamSys.TryAddToBloodstream((human, stream), morphine), Is.True);
        });

        await Pair.RunTicksSync(90);

        await Server.WaitAssertion(() =>
        {
            var statusSys = Server.EntMan.System<StatusEffectsSystem>();
            Assert.That(
                statusSys.HasStatusEffect(human, "StatusEffectDrowsiness"),
                Is.True,
                "Morphine should apply drowsiness via StatusEffectDrowsiness at >=12u.");
        });
    }
}
