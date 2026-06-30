using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Shared.Backmen.Body.Systems;
using Content.Shared.Backmen.CCVar;
using Content.Shared.Backmen.Surgery.Traumas.Components;
using Content.Shared.Backmen.Surgery.Wounds.Components;
using Content.Shared.Backmen.Surgery.Wounds.Systems;
using Content.Shared.Backmen.Targeting;
using Content.Shared.Body.Part;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Medical;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Backmen.Medical;

[TestFixture]
[EnsureCVar(Side.Server, typeof(CCVars), nameof(CCVars.PainEnabled), true)]
public sealed class HealingFallbackTest : GameTest
{
    private static readonly EntProtoId MobHuman = "MobHuman";

    public override PoolSettings PoolSettings => new()
    {
        Connected = true,
        Dirty = true,
    };

    [Test]
    public async Task Ointment_HealsColdOnOtherPart_WhenTargetedPartHasNoCold()
    {
        var map = await Pair.CreateTestMap();
        var damageSys = Server.EntMan.System<DamageableSystem>();
        var woundSys = Server.EntMan.System<WoundSystem>();
        var bodySys = Server.EntMan.System<BkmBodySharedSystem>();
        EntityUid patient = default;
        EntityUid healer = default;
        EntityUid ointment = default;

        await Server.WaitPost(() =>
        {
            patient = Server.EntMan.SpawnAtPosition(MobHuman, map.GridCoords);
            healer = Server.EntMan.SpawnAtPosition(MobHuman, map.GridCoords);
            ointment = Server.EntMan.Spawn("Ointment");

            var cold = new DamageSpecifier { DamageDict = { ["Cold"] = FixedPoint2.New(4) } };
            damageSys.ChangeDamage(patient, cold, targetPart: TargetBodyPart.Chest);

            Assert.That(bodySys.TryGetWoundableTargetByType(patient, BodyPartType.Chest, null, out var chest), Is.True);
            Assert.That(woundSys.HasDamageOfType(chest, "Cold"), Is.True);

            var targeting = Server.EntMan.EnsureComponent<TargetingComponent>(healer);
            targeting.Target = TargetBodyPart.Head;
        });

        await RaiseHealingDoAfter(patient, healer, ointment);

        await Server.WaitAssertion(() =>
        {
            Assert.That(bodySys.TryGetWoundableTargetByType(patient, BodyPartType.Chest, null, out var chest), Is.True);
            Assert.That(woundSys.HasDamageOfType(chest, "Cold"), Is.False);
        });
    }

    [Test]
    public async Task Ointment_HealsCold_WhenHeavyBleedingOnSamePart()
    {
        var map = await Pair.CreateTestMap();
        var damageSys = Server.EntMan.System<DamageableSystem>();
        var woundSys = Server.EntMan.System<WoundSystem>();
        var bodySys = Server.EntMan.System<BkmBodySharedSystem>();
        EntityUid patient = default;
        EntityUid healer = default;
        EntityUid ointment = default;

        await Server.WaitPost(() =>
        {
            patient = Server.EntMan.SpawnAtPosition(MobHuman, map.GridCoords);
            healer = Server.EntMan.SpawnAtPosition(MobHuman, map.GridCoords);
            ointment = Server.EntMan.Spawn("Ointment");

            var cold = new DamageSpecifier { DamageDict = { ["Cold"] = FixedPoint2.New(4) } };
            damageSys.ChangeDamage(patient, cold, targetPart: TargetBodyPart.Chest);

            Assert.That(bodySys.TryGetWoundableTargetByType(patient, BodyPartType.Chest, null, out var chest), Is.True);
            Assert.That(Server.EntMan.TryGetComponent(chest, out WoundableComponent? chestWoundable), Is.True);
            Assert.That(woundSys.HasDamageOfType(chest, "Cold"), Is.True);

            // Induce light slash bleeding without trauma, then crank bleed above the ointment threshold (4.5).
            Assert.That(woundSys.TryInduceWound(chest, "Slash", FixedPoint2.New(5), out _, chestWoundable), Is.True);
            foreach (var wound in woundSys.GetWoundableWoundsWithComp<BleedInflicterComponent>(chest, chestWoundable))
            {
                wound.Comp2.BleedingAmountRaw = FixedPoint2.New(10);
                wound.Comp2.IsBleeding = true;
                wound.Comp2.Scaling = FixedPoint2.New(1);
            }

            var targeting = Server.EntMan.EnsureComponent<TargetingComponent>(healer);
            targeting.Target = TargetBodyPart.Chest;
        });

        await RaiseHealingDoAfter(patient, healer, ointment);

        await Server.WaitAssertion(() =>
        {
            Assert.That(bodySys.TryGetWoundableTargetByType(patient, BodyPartType.Chest, null, out var chest), Is.True);
            Assert.That(woundSys.HasDamageOfType(chest, "Cold"), Is.False);
        });
    }

    private async Task RaiseHealingDoAfter(EntityUid patient, EntityUid healer, EntityUid ointment)
    {
        await Server.WaitPost(() =>
        {
            var doAfterSys = Server.EntMan.System<SharedDoAfterSystem>();
            var healingEv = new HealingDoAfterEvent();
            var args = new DoAfterArgs(Server.EntMan, healer, TimeSpan.Zero, healingEv, patient, patient, ointment)
            {
                EventTarget = patient,
                NeedHand = false,
                RequireCanInteract = false,
            };

            Assert.That(doAfterSys.TryStartDoAfter(args), Is.True);
        });
    }
}
