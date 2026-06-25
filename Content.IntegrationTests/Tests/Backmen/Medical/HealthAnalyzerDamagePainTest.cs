using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server.Backmen.Surgery.Consciousness.Systems;
using Content.Server.Backmen.Surgery.Pain.Systems;
using Content.Shared.Backmen.CCVar;
using Content.Shared.Backmen.Surgery.Consciousness.Components;
using Content.Shared.Backmen.Surgery.Pain.Components;
using Content.Shared.Backmen.Surgery.Wounds.Components;
using Content.Shared.Backmen.Surgery.Wounds.Systems;
using Content.Shared.Backmen.Targeting;
using Content.Shared.Body;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Backmen.Medical;

/// <summary>
/// Wound severity shown in the health analyzer must stay proportional to nerve pain after nubody port.
/// </summary>
[TestFixture]
[EnsureCVar(Side.Server, typeof(CCVars), nameof(CCVars.PainEnabled), true)]
public sealed class HealthAnalyzerDamagePainTest : GameTest
{
    private static readonly EntProtoId MobHuman = "MobHuman";

    public override PoolSettings PoolSettings => new()
    {
        Connected = true,
        Dirty = true,
    };

    [Test]
    public async Task GunshotHead_IncreasesWoundSeverityAndPainProportionally()
    {
        var map = await Pair.CreateTestMap();
        EntityUid human = default;

        await Server.WaitPost(() =>
        {
            human = Server.EntMan.SpawnAtPosition(MobHuman, map.GridCoords);

            var damageSys = Server.EntMan.System<DamageableSystem>();
            var damage = new DamageSpecifier { DamageDict = { ["Piercing"] = 9 } };
            Assert.That(
                damageSys.ChangeDamage(human, damage, targetPart: TargetBodyPart.Head).GetTotal() > 0,
                Is.True);

            var woundSys = Server.EntMan.System<WoundSystem>();
            var severity = woundSys.GetBodySeverityPoint(human);
            Assert.That(severity, Is.EqualTo(FixedPoint2.New(9)));

            Assert.That(Server.EntMan.TryGetComponent(human, out ConsciousnessComponent? consciousness), Is.True);
            Assert.That(consciousness!.NerveSystem, Is.Not.Null);

            var nerveSys = Server.EntMan.GetComponent<NerveSystemComponent>(consciousness.NerveSystem.Value);
            Assert.That((float) nerveSys.Pain, Is.LessThanOrEqualTo((float) severity * 2f),
                "Pain should stay in the same order of magnitude as wound severity.");

            foreach (var wound in woundSys.GetBodyWounds(human, Server.EntMan.GetComponent<BodyComponent>(human)))
            {
                if (!Server.EntMan.TryGetComponent<PainInflicterComponent>(wound, out var inflicter))
                    continue;

                Assert.That(inflicter.RawPain, Is.LessThanOrEqualTo(FixedPoint2.New(100)),
                    "RawPain must respect inflicter cap.");
                Assert.That(inflicter.RawPain, Is.LessThanOrEqualTo(wound.Comp.WoundSeverityPoint),
                    "RawPain must not exceed wound severity.");
            }
        });
    }

    [Test]
    public async Task WoundSeverityCap_StopsGrowingAndPainStaysStable()
    {
        var map = await Pair.CreateTestMap();
        EntityUid human = default;
        FixedPoint2 severityAfterCap = FixedPoint2.Zero;
        float painAfterCap;

        await Server.WaitPost(() =>
        {
            human = Server.EntMan.SpawnAtPosition(MobHuman, map.GridCoords);
            var damageSys = Server.EntMan.System<DamageableSystem>();
            var piercing = new DamageSpecifier { DamageDict = { ["Piercing"] = FixedPoint2.New(50) } };

            for (var i = 0; i < 10; i++)
                damageSys.ChangeDamage(human, piercing, targetPart: TargetBodyPart.Head);

            var woundSys = Server.EntMan.System<WoundSystem>();
            severityAfterCap = woundSys.GetBodySeverityPoint(human);
            Assert.That(severityAfterCap, Is.GreaterThanOrEqualTo(FixedPoint2.New(200)),
                "Head piercing should reach wound severity cap.");

            var consciousness = Server.EntMan.GetComponent<ConsciousnessComponent>(human);
            painAfterCap = (float) Server.EntMan.GetComponent<NerveSystemComponent>(consciousness.NerveSystem!.Value).Pain;

            for (var i = 0; i < 10; i++)
                damageSys.ChangeDamage(human, piercing, targetPart: TargetBodyPart.Head);

            Assert.That(woundSys.GetBodySeverityPoint(human), Is.EqualTo(severityAfterCap),
                "Severity must not grow past cap.");

            var painAfterMore = (float) Server.EntMan.GetComponent<NerveSystemComponent>(consciousness.NerveSystem!.Value).Pain;
            Assert.That(painAfterMore, Is.EqualTo(painAfterCap).Within(5f),
                "Pain must not balloon after severity cap.");

            foreach (var wound in woundSys.GetBodyWounds(human, Server.EntMan.GetComponent<BodyComponent>(human)))
            {
                if (!Server.EntMan.TryGetComponent<PainInflicterComponent>(wound, out var inflicter))
                    continue;

                Assert.That(inflicter.RawPain, Is.LessThanOrEqualTo(wound.Comp.WoundSeverityPoint));
            }
        });
    }

    [Test]
    public async Task GetBodySeverityPoint_MatchesWoundSum()
    {
        var map = await Pair.CreateTestMap();
        EntityUid human = default;

        await Server.WaitPost(() =>
        {
            human = Server.EntMan.SpawnAtPosition(MobHuman, map.GridCoords);

            var damageSys = Server.EntMan.System<DamageableSystem>();
            damageSys.ChangeDamage(
                human,
                new DamageSpecifier { DamageDict = { ["Piercing"] = 27 } },
                targetPart: TargetBodyPart.Head);

            var woundSys = Server.EntMan.System<WoundSystem>();
            var body = Server.EntMan.GetComponent<BodyComponent>(human);
            var fromWounds = woundSys.GetBodyWounds(human, body)
                .Where(w => !w.Comp.IsScar)
                .Aggregate(FixedPoint2.Zero, (sum, w) => sum + w.Comp.WoundSeverityPoint);

            Assert.That(woundSys.GetBodySeverityPoint(human), Is.EqualTo(fromWounds));
        });
    }

    [Test]
    public async Task OrganDamagePain_NotDuplicatedAcrossWoundables()
    {
        var map = await Pair.CreateTestMap();
        EntityUid human = default;

        await Server.WaitPost(() =>
        {
            human = Server.EntMan.SpawnAtPosition(MobHuman, map.GridCoords);

            var damageSys = Server.EntMan.System<DamageableSystem>();
            for (var i = 0; i < 15; i++)
            {
                damageSys.ChangeDamage(
                    human,
                    new DamageSpecifier { DamageDict = { ["Piercing"] = 20 } },
                    targetPart: TargetBodyPart.Head);
            }

            var consciousnessSys = Server.EntMan.System<ServerConsciousnessSystem>();
            var painCauses = consciousnessSys.GetPainCauses(human);
            Assert.That(painCauses, Is.Not.Null);

            var organDamageEntries = painCauses!
                .Where(kv => kv.Key == "OrganDamage")
                .ToList();

            Assert.That(organDamageEntries.Count, Is.LessThanOrEqualTo(1),
                "OrganDamage pain must be a single nerve-system modifier, not one per woundable.");

            if (organDamageEntries.Count == 1)
            {
                var totalPain = consciousnessSys.GetTotalPain(human) ?? 0f;
                Assert.That(organDamageEntries[0].Value, Is.LessThanOrEqualTo(totalPain + 1f));
            }
        });
    }
}
