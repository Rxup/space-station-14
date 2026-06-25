using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server.Backmen.Body;
using Content.Server.Backmen.Body.Systems;
using Content.Server.Destructible;
using Content.Server.Destructible.Thresholds.Behaviors;
using Content.Shared.Backmen.Surgery;
using Content.Shared.Backmen.Targeting;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Destructible.Thresholds.Triggers;
using Content.Shared.FixedPoint;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.Backmen.Body;

[TestFixture]
public sealed class SurgeryGibThresholdTest : GameTest
{
    public override PoolSettings PoolSettings => new()
    {
        Dirty = true,
        Connected = false,
        InLobby = false,
    };

    [Test]
    public async Task MobHuman_FourMeleeDebug100Hits_DoNotFullBodyGib_ButThousandDoes()
    {
        var entMan = Server.ResolveDependency<IEntityManager>();
        var damageable = entMan.System<DamageableSystem>();
        var bodySystem = entMan.System<BkmBodySystem>();
        var gibSys = entMan.System<BkmSurgeryDestructibleSystem>();

        var testMap = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var patient = entMan.Spawn("MobHuman", testMap.MapCoords);
            var initialWoundables = bodySystem.GetWoundableTargets(patient).Count();

            Assert.That(entMan.HasComponent<SurgeryTargetComponent>(patient));
            Assert.That(entMan.TryGetComponent(patient, out DestructibleComponent? destructible));

            var gibThresholds = destructible!.Thresholds
                .Where(t => t.Behaviors.Any(b => b is GibBehavior))
                .ToList();

            Assert.That(gibThresholds, Has.Count.EqualTo(1));
            Assert.That(gibThresholds[0].Trigger, Is.InstanceOf<DamageTrigger>());
            Assert.That(((DamageTrigger) gibThresholds[0].Trigger!).Damage,
                Is.EqualTo(BkmSurgeryDestructibleSystem.SurgeryGibTotalDamage));

            var blunt = new DamageSpecifier();
            blunt.DamageDict["Blunt"] = FixedPoint2.New(100);

            for (var i = 0; i < 4; i++)
            {
                damageable.ChangeDamage(patient, blunt, targetPart: TargetBodyPart.All);
                Assert.That(entMan.EntityExists(patient), $"Patient should survive hit {i + 1}");
                Assert.That(
                    gibSys.ShouldFullBodyGib(patient),
                    Is.False,
                    $"Four-hundred blunt should not trigger full-body gib (hit {i + 1})");
            }

            Assert.That(
                gibSys.GetAccumulatedGibDamage(patient),
                Is.GreaterThanOrEqualTo(FixedPoint2.New(400)));

            blunt.DamageDict["Blunt"] = FixedPoint2.New(600);
            damageable.ChangeDamage(patient, blunt, targetPart: TargetBodyPart.All);

            Assert.That(gibSys.ShouldFullBodyGib(patient), Is.True);
            Assert.That(
                bodySystem.GetWoundableTargets(patient).Count(),
                Is.LessThan(initialWoundables),
                "Reaching 1000 total wound damage should full-body gib");
        });
    }
}
