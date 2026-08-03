using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Shared.Backmen.Body.Systems;
using Content.Shared.Backmen.CCVar;
using Content.Shared.Backmen.Medical;
using Content.Shared.Backmen.Surgery.Consciousness.Components;
using Content.Shared.Backmen.Surgery.Traumas.Components;
using Content.Shared.Backmen.Surgery.Wounds.Components;
using Content.Shared.Backmen.Surgery.Wounds.Systems;
using Content.Shared.Backmen.Targeting;
using Content.Shared.Body;
using Content.Shared.Body.Part;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Medical;
using Content.Shared.Medical.Healing;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Backmen.Medical;

[TestFixture]
[EnsureCVar(Side.Server, typeof(CCVars), nameof(CCVars.PainEnabled), true)]
public sealed class HealingFallbackTest : GameTest
{
    private static readonly EntProtoId MobHuman = "MobHuman";
    private static readonly EntProtoId MobCorgi = "MobCorgi";

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

    [Test]
    public async Task Ointment_HealsHead_WhenHealerHasNoTargetingComponent()
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

            Server.EntMan.RemoveComponent<TargetingComponent>(healer);
            Assert.That(Server.EntMan.HasComponent<TargetingComponent>(healer), Is.False);

            var cold = new DamageSpecifier { DamageDict = { ["Cold"] = FixedPoint2.New(4) } };
            damageSys.ChangeDamage(patient, cold, targetPart: TargetBodyPart.Head);

            Assert.That(bodySys.TryGetWoundableTargetByType(patient, BodyPartType.Head, null, out var head), Is.True);
            Assert.That(woundSys.HasDamageOfType(head, "Cold"), Is.True);
        });

        await RaiseHealingDoAfter(patient, healer, ointment);

        await Server.WaitAssertion(() =>
        {
            Assert.That(bodySys.TryGetWoundableTargetByType(patient, BodyPartType.Head, null, out var head), Is.True);
            Assert.That(woundSys.HasDamageOfType(head, "Cold"), Is.False);
        });
    }

    [Test]
    public async Task Brutepack_HealsLeftArm_WhenHeadSelectedAndArmDamaged()
    {
        var map = await Pair.CreateTestMap();
        var damageSys = Server.EntMan.System<DamageableSystem>();
        var woundSys = Server.EntMan.System<WoundSystem>();
        var bodySys = Server.EntMan.System<BkmBodySharedSystem>();
        EntityUid patient = default;
        EntityUid healer = default;
        EntityUid brutepack = default;

        await Server.WaitPost(() =>
        {
            patient = Server.EntMan.SpawnAtPosition(MobHuman, map.GridCoords);
            healer = Server.EntMan.SpawnAtPosition(MobHuman, map.GridCoords);
            brutepack = Server.EntMan.Spawn("Brutepack");

            // Brutepack heals 3.75 blunt per use — keep damage under that so one use clears the part.
            var blunt = new DamageSpecifier { DamageDict = { ["Blunt"] = FixedPoint2.New(3) } };
            damageSys.ChangeDamage(patient, blunt, targetPart: TargetBodyPart.LeftArm);

            Assert.That(
                bodySys.TryGetWoundableTargetByType(patient, BodyPartType.Arm, BodyPartSymmetry.Left, out var arm),
                Is.True);
            Assert.That(woundSys.HasDamageOfType(arm, "Blunt"), Is.True);

            var targeting = Server.EntMan.EnsureComponent<TargetingComponent>(healer);
            targeting.Target = TargetBodyPart.Head;
        });

        await RaiseHealingDoAfter(patient, healer, brutepack);

        await Server.WaitAssertion(() =>
        {
            Assert.That(
                bodySys.TryGetWoundableTargetByType(patient, BodyPartType.Arm, BodyPartSymmetry.Left, out var arm),
                Is.True);
            Assert.That(woundSys.HasDamageOfType(arm, "Blunt"), Is.False);

            Assert.That(bodySys.TryGetWoundableTargetByType(patient, BodyPartType.Head, null, out var head), Is.True);
            Assert.That(woundSys.HasDamageOfType(head, "Blunt"), Is.False);
        });
    }

    [Test]
    public async Task Ointment_ReResolves_WhenCachedWoundableAlreadyHealed()
    {
        var map = await Pair.CreateTestMap();
        var damageSys = Server.EntMan.System<DamageableSystem>();
        var woundSys = Server.EntMan.System<WoundSystem>();
        var bodySys = Server.EntMan.System<BkmBodySharedSystem>();
        EntityUid patient = default;
        EntityUid healer = default;
        EntityUid ointment = default;
        NetEntity staleHead = default;

        await Server.WaitPost(() =>
        {
            patient = Server.EntMan.SpawnAtPosition(MobHuman, map.GridCoords);
            healer = Server.EntMan.SpawnAtPosition(MobHuman, map.GridCoords);
            ointment = Server.EntMan.Spawn("Ointment");

            var cold = new DamageSpecifier { DamageDict = { ["Cold"] = FixedPoint2.New(4) } };
            damageSys.ChangeDamage(patient, cold, targetPart: TargetBodyPart.Head);
            damageSys.ChangeDamage(patient, cold, targetPart: TargetBodyPart.Chest);

            Assert.That(bodySys.TryGetWoundableTargetByType(patient, BodyPartType.Head, null, out var head), Is.True);
            Assert.That(bodySys.TryGetWoundableTargetByType(patient, BodyPartType.Chest, null, out var chest), Is.True);
            Assert.That(woundSys.HasDamageOfType(head, "Cold"), Is.True);
            Assert.That(woundSys.HasDamageOfType(chest, "Cold"), Is.True);
            staleHead = Server.EntMan.GetNetEntity(head);

            var targeting = Server.EntMan.EnsureComponent<TargetingComponent>(healer);
            targeting.Target = TargetBodyPart.Head;
        });

        // First use clears the aimed head.
        await RaiseHealingDoAfter(patient, healer, ointment);

        await Server.WaitAssertion(() =>
        {
            Assert.That(bodySys.TryGetWoundableTargetByType(patient, BodyPartType.Head, null, out var head), Is.True);
            Assert.That(woundSys.HasDamageOfType(head, "Cold"), Is.False);
            Assert.That(bodySys.TryGetWoundableTargetByType(patient, BodyPartType.Chest, null, out var chest), Is.True);
            Assert.That(woundSys.HasDamageOfType(chest, "Cold"), Is.True);
        });

        // Simulate a Repeat tick that still carries the stale healed TargetWoundable.
        await Server.WaitPost(() =>
        {
            var doAfterSys = Server.EntMan.System<SharedDoAfterSystem>();
            var healingComp = Server.EntMan.GetComponent<HealingComponent>(ointment);
            var healingEv = new HealingDoAfterEvent { TargetWoundable = staleHead };

            var args = new DoAfterArgs(Server.EntMan, healer, TimeSpan.Zero, healingEv, patient, patient, ointment)
            {
                EventTarget = patient,
                NeedHand = false,
                RequireCanInteract = false,
            };

            Assert.That(doAfterSys.TryStartDoAfter(args), Is.True);
        });

        await Server.WaitAssertion(() =>
        {
            Assert.That(bodySys.TryGetWoundableTargetByType(patient, BodyPartType.Chest, null, out var chest), Is.True);
            Assert.That(woundSys.HasDamageOfType(chest, "Cold"), Is.False);
        });
    }

    [Test]
    public async Task Brutepack_HealsInjurableBody_WithoutConsciousness()
    {
        var map = await Pair.CreateTestMap();
        var damageSys = Server.EntMan.System<DamageableSystem>();
        EntityUid patient = default;
        EntityUid healer = default;
        EntityUid brutepack = default;
        FixedPoint2 damageBefore = default;

        await Server.WaitPost(() =>
        {
            patient = Server.EntMan.SpawnAtPosition(MobCorgi, map.GridCoords);
            healer = Server.EntMan.SpawnAtPosition(MobHuman, map.GridCoords);
            brutepack = Server.EntMan.Spawn("Brutepack");

            Assert.That(Server.EntMan.HasComponent<BodyComponent>(patient), Is.True);
            Assert.That(Server.EntMan.HasComponent<InjurableComponent>(patient), Is.True);
            Assert.That(Server.EntMan.HasComponent<ConsciousnessComponent>(patient), Is.False);

            var blunt = new DamageSpecifier { DamageDict = { ["Blunt"] = FixedPoint2.New(10) } };
            damageSys.ChangeDamage(patient, blunt);
            damageBefore = damageSys.GetTotalDamage(patient);
            Assert.That(damageBefore, Is.GreaterThan(FixedPoint2.Zero));
        });

        await RaiseHealingDoAfter(patient, healer, brutepack);

        await Server.WaitAssertion(() =>
        {
            var damageAfter = damageSys.GetTotalDamage(patient);
            Assert.That(damageAfter, Is.LessThan(damageBefore));
        });
    }

    private async Task RaiseHealingDoAfter(EntityUid patient, EntityUid healer, EntityUid item)
    {
        await Server.WaitPost(() =>
        {
            var doAfterSys = Server.EntMan.System<SharedDoAfterSystem>();
            var medicalTargetSys = Server.EntMan.System<BackmenMedicalTargetSystem>();
            var healingComp = Server.EntMan.GetComponent<HealingComponent>(item);

            var healingEv = new HealingDoAfterEvent();
            if (Server.EntMan.HasComponent<ConsciousnessComponent>(patient)
                && medicalTargetSys.TryResolveHealTarget(patient, healer, healingComp, out var woundable, out _, out _))
            {
                healingEv.TargetWoundable = Server.EntMan.GetNetEntity(woundable);
            }

            var args = new DoAfterArgs(Server.EntMan, healer, TimeSpan.Zero, healingEv, patient, patient, item)
            {
                EventTarget = patient,
                NeedHand = false,
                RequireCanInteract = false,
            };

            Assert.That(doAfterSys.TryStartDoAfter(args), Is.True);
        });
    }
}
