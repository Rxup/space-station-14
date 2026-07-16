using Content.IntegrationTests.Fixtures;
using Content.Server.Backmen.Targeting;
using Content.Shared.Backmen.Surgery.Consciousness.Components;
using Content.Shared.Backmen.Targeting;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Server.Implants;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Random.Helpers;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests.Backmen.Combat;

[TestFixture]
public sealed class RangedTargetingTest : GameTest
{
    private static readonly EntProtoId MobHuman = "MobHuman";
    private static readonly EntProtoId ArmorVest = "ClothingOuterArmorBasic";
    private static readonly EntProtoId XenoborgHeavy = "XenoborgHeavy";
    private static readonly EntProtoId CombatTrainingImplant = "CombatTrainingImplant";
    private static readonly EntProtoId WeaponPistol = "WeaponPistolMk58";

    public override PoolSettings PoolSettings => new()
    {
        Connected = true,
        Dirty = true,
    };

    [Test]
    public async Task ResolveCombatTargetOddsProto_Tiers()
    {
        await Server.WaitAssertion(() =>
        {
            var entMan = Server.EntMan;
            var targeting = entMan.System<TargetingSystem>();
            var mindSys = entMan.System<SharedMindSystem>();
            var roleSys = entMan.System<SharedRoleSystem>();

            var civilian = entMan.Spawn(MobHuman);
            entMan.EnsureComponent<TargetingComponent>(civilian);
            Assert.That(targeting.ResolveCombatTargetOddsProto(civilian), Is.EqualTo(new ProtoId<CombatTargetOddsPrototype>("Default")));

            var secOfficer = entMan.Spawn(MobHuman);
            entMan.EnsureComponent<TargetingComponent>(secOfficer);
            entMan.EnsureComponent<MindContainerComponent>(secOfficer);
            var secMind = mindSys.CreateMind(null);
            mindSys.TransferTo(secMind, secOfficer);
            roleSys.MindAddJobRole(secMind, jobPrototype: "SecurityOfficer");
            Assert.That(targeting.ResolveCombatTargetOddsProto(secOfficer), Is.EqualTo(new ProtoId<CombatTargetOddsPrototype>("Security")));

            var elite = entMan.Spawn(MobHuman);
            entMan.EnsureComponent<TargetingComponent>(elite);
            entMan.EnsureComponent<MindContainerComponent>(elite);
            var eliteMind = mindSys.CreateMind(null);
            mindSys.TransferTo(eliteMind, elite);
            roleSys.MindAddJobRole(eliteMind, jobPrototype: "DeathSquad");
            Assert.That(targeting.ResolveCombatTargetOddsProto(elite), Is.EqualTo(new ProtoId<CombatTargetOddsPrototype>("Elite")));

            var xenoborg = entMan.Spawn(XenoborgHeavy);
            Assert.That(targeting.ResolveCombatTargetOddsProto(xenoborg), Is.EqualTo(new ProtoId<CombatTargetOddsPrototype>("Elite")));

            var overridden = entMan.Spawn(MobHuman);
            var oddsOverride = entMan.EnsureComponent<CombatTargetOddsOverrideComponent>(overridden);
            oddsOverride.Odds = "Admin";
            Assert.That(targeting.ResolveCombatTargetOddsProto(overridden), Is.EqualTo(new ProtoId<CombatTargetOddsPrototype>("Admin")));
        });
    }

    [Test]
    public async Task TryResolveCombatBodyPart_Gates()
    {
        var map = await Pair.CreateTestMap();
        await Pair.RunTicksSync(5);

        await Server.WaitPost(() =>
        {
            var entMan = Server.EntMan;
            var targeting = entMan.System<TargetingSystem>();

            var victim = entMan.SpawnAtPosition(MobHuman, map.GridCoords);
            var shooterWithoutTargeting = entMan.SpawnAtPosition(MobHuman, map.GridCoords);
            var shooterWithTargeting = entMan.SpawnAtPosition(MobHuman, map.GridCoords);
            entMan.RemoveComponent<TargetingComponent>(shooterWithoutTargeting);

            Assert.That(entMan.TryGetComponent(victim, out ConsciousnessComponent? _), Is.True);
            Assert.That(targeting.TryResolveCombatBodyPart(victim, shooterWithoutTargeting, null, out _), Is.False);
            Assert.That(targeting.TryResolveCombatBodyPart(victim, shooterWithTargeting, shooterWithTargeting, out var hitPart), Is.True);
            Assert.That(Enum.IsDefined(hitPart), Is.True);
        });
    }

    [Test]
    public async Task TryResolveCombatBodyPart_SeedGunFallback()
    {
        var map = await Pair.CreateTestMap();
        await Pair.RunTicksSync(5);

        await Server.WaitPost(() =>
        {
            var entMan = Server.EntMan;
            var targeting = entMan.System<TargetingSystem>();
            var handsSys = entMan.System<SharedHandsSystem>();

            var victim = entMan.SpawnAtPosition(MobHuman, map.GridCoords);
            var gunHolder = entMan.SpawnAtPosition(MobHuman, map.GridCoords);
            var gun = entMan.SpawnAtPosition(WeaponPistol, map.GridCoords);
            entMan.EnsureComponent<TargetingComponent>(gunHolder);

            Assert.That(handsSys.TryPickupAnyHand(gunHolder, gun, checkActionBlocker: false), Is.True);
            Assert.That(targeting.TryResolveCombatBodyPart(victim, null, gun, out var hitPart), Is.True);
            Assert.That(Enum.IsDefined(hitPart), Is.True);
        });
    }

    [Test]
    public async Task PredictedRandom_IsDeterministic()
    {
        await Server.WaitAssertion(() =>
        {
            var entMan = Server.EntMan;
            var targeting = entMan.System<TargetingSystem>();
            var timing = Server.ResolveDependency<IGameTiming>();

            var victim = entMan.Spawn(MobHuman);
            var seed = entMan.Spawn(MobHuman);

            Assert.That(targeting.TryGetCombatTargetOddsSpread("Default", TargetBodyPart.Head, out var weights), Is.True);

            var netVictim = entMan.GetNetEntity(victim);
            var netSeed = entMan.GetNetEntity(seed);

            var first = SharedRandomExtensions.Pick(weights, SharedRandomExtensions.PredictedRandom(timing, netVictim, netSeed));
            var second = SharedRandomExtensions.Pick(weights, SharedRandomExtensions.PredictedRandom(timing, netVictim, netSeed));

            Assert.That(first, Is.EqualTo(second));
        });
    }

    [Test]
    public async Task SpreadTiers_SelfHitWeights()
    {
        await Server.WaitAssertion(() =>
        {
            var targeting = Server.EntMan.System<TargetingSystem>();

            Assert.That(targeting.TryGetCombatTargetOddsSpread("Default", TargetBodyPart.Chest, out var defaultSpread), Is.True);
            Assert.That(defaultSpread[TargetBodyPart.Chest], Is.EqualTo(0.17f).Within(0.01f));

            Assert.That(targeting.TryGetCombatTargetOddsSpread("Security", TargetBodyPart.Chest, out var securitySpread), Is.True);
            Assert.That(securitySpread[TargetBodyPart.Chest], Is.EqualTo(0.37f).Within(0.01f));

            Assert.That(targeting.TryGetCombatTargetOddsSpread("Elite", TargetBodyPart.Chest, out var eliteSpread), Is.True);
            Assert.That(eliteSpread[TargetBodyPart.Chest], Is.EqualTo(0.65f).Within(0.01f));

            Assert.That(targeting.TryGetCombatTargetOddsSpread("Admin", TargetBodyPart.Chest, out var adminSpread), Is.True);
            Assert.That(adminSpread[TargetBodyPart.Chest], Is.EqualTo(1f).Within(1e-4f));
        });
    }

    [Test]
    public async Task CombatTrainingImplant_AppliesSecurityOverride()
    {
        await Server.WaitAssertion(() =>
        {
            var entMan = Server.EntMan;
            var implantSys = entMan.System<SubdermalImplantSystem>();

            var host = entMan.Spawn(MobHuman);
            var implant = entMan.Spawn(CombatTrainingImplant);

            implantSys.ForceImplant(host, implant);

            Assert.That(entMan.TryGetComponent(host, out CombatTargetOddsOverrideComponent? oddsOverride), Is.True);
            Assert.That(oddsOverride!.Odds, Is.EqualTo(new ProtoId<CombatTargetOddsPrototype>("Security")));
            Assert.That(oddsOverride.FromImplant, Is.True);
        });
    }

    [Test]
    public async Task AutoResolvedHit_AppliesArmorToChest()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitRunTicks(2);

        await Server.WaitPost(() =>
        {
            var entMan = Server.EntMan;
            var damageSys = entMan.System<DamageableSystem>();
            var invSys = entMan.System<InventorySystem>();

            var shooter = entMan.SpawnAtPosition(MobHuman, map.GridCoords);
            var armoredVictim = entMan.SpawnAtPosition(MobHuman, map.GridCoords);
            var vest = entMan.SpawnEntity(ArmorVest, map.MapCoords);

            var shooterTargeting = entMan.EnsureComponent<TargetingComponent>(shooter);
            shooterTargeting.Target = TargetBodyPart.Chest;
            var shooterOverride = entMan.EnsureComponent<CombatTargetOddsOverrideComponent>(shooter);
            shooterOverride.Odds = "Admin";
            Assert.That(invSys.TryEquip(armoredVictim, vest, "outerClothing"), Is.True);

            var damage = new DamageSpecifier { DamageDict = { ["Blunt"] = FixedPoint2.New(10) } };
            var autoDamage = damageSys.ChangeDamage(armoredVictim, damage, origin: shooter, seedEntity: shooter).GetTotal();

            Assert.That(autoDamage, Is.EqualTo(FixedPoint2.New(7)),
                "Combat auto-resolved chest hits must stack chest armor before wound routing.");
        });
    }
}
