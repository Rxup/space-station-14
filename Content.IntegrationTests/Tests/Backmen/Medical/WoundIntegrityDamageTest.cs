using Content.IntegrationTests.Fixtures;
using Content.Shared.Backmen.Body.Systems;
using Content.Shared.Backmen.Surgery.Wounds.Systems;
using Content.Shared.Body;
using Content.Shared.Body.Part;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Backmen.Targeting;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Backmen.Medical;

[TestFixture]
public sealed class WoundIntegrityDamageTest : GameTest
{
    private static readonly EntProtoId MobHuman = "MobHuman";

    /// <summary>
    /// Anti-tank piercing after damage-type aliases (matches BulletAntitank).
    /// </summary>
    private static readonly DamageSpecifier AntitankPiercing = new()
    {
        DamageDict =
        {
            ["Piercing"] = FixedPoint2.New(300),
        },
    };

    public override PoolSettings PoolSettings => new()
    {
        Connected = true,
        Dirty = true,
    };

    [Test]
    public async Task PiercingDamage_AccumulatesBeyondFormerWoundCap()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitPost(() =>
        {
            var entMan = Server.EntMan;
            var damageSys = entMan.System<DamageableSystem>();
            var woundSys = entMan.System<WoundSystem>();
            var bkmBodySys = entMan.System<BkmBodySharedSystem>();

            var patient = entMan.SpawnAtPosition(MobHuman, map.GridCoords);

            damageSys.ChangeDamage(patient, AntitankPiercing, targetPart: TargetBodyPart.Chest);

            Assert.That(bkmBodySys.TryGetWoundableTargetByType(
                patient,
                BodyPartType.Chest,
                null,
                out var chest), Is.True);

            Assert.That(woundSys.GetWoundableSeverityPoint(chest), Is.EqualTo(FixedPoint2.New(300)));
        });
    }

    [Test]
    public async Task AntitankPiercing_OneShotDestroysLeftLeg()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitPost(() =>
        {
            var entMan = Server.EntMan;
            var damageSys = entMan.System<DamageableSystem>();
            var bodySys = entMan.System<BodySystem>();

            var patient = entMan.SpawnAtPosition(MobHuman, map.GridCoords);

            damageSys.ChangeDamage(patient, AntitankPiercing, targetPart: TargetBodyPart.LeftLeg);

            Assert.That(bodySys.TryGetOrganByCategory(patient, "LegLeft", out _), Is.False,
                "Anti-tank piercing should destroy a leg in a single hit via integrity loss.");
        });
    }

    [Test]
    public async Task AntitankPiercing_OneShotDestroysHead()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitPost(() =>
        {
            var entMan = Server.EntMan;
            var damageSys = entMan.System<DamageableSystem>();
            var bodySys = entMan.System<BodySystem>();

            var patient = entMan.SpawnAtPosition(MobHuman, map.GridCoords);

            damageSys.ChangeDamage(patient, AntitankPiercing, targetPart: TargetBodyPart.Head);

            Assert.That(bodySys.TryGetOrganByCategory(patient, "Head", out _), Is.False,
                "Anti-tank piercing should destroy an unarmored head in a single hit via integrity loss.");
        });
    }
}
