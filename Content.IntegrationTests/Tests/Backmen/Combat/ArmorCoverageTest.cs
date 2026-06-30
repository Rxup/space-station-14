using Content.IntegrationTests.Fixtures;
using Content.Shared.Backmen.Surgery.Wounds.Systems;
using Content.Shared.Backmen.Targeting;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Inventory;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Backmen.Combat;

[TestFixture]
public sealed class ArmorCoverageTest : GameTest
{
    private static readonly EntProtoId MobHuman = "MobHuman";
    private static readonly EntProtoId ArmorVest = "ClothingOuterArmorBasic";

    public override PoolSettings PoolSettings => new()
    {
        Connected = true,
        Dirty = true,
    };

    [Test]
    public async Task ChestArmor_ReducesDamageByCoverage()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitRunTicks(2);

        await Server.WaitPost(() =>
        {
            var entMan = Server.EntMan;
            var damageSys = entMan.System<DamageableSystem>();
            var invSys = entMan.System<InventorySystem>();
            var woundSys = entMan.System<WoundSystem>();

            var unarmored = entMan.SpawnAtPosition(MobHuman, map.GridCoords);
            var headArmoredHuman = entMan.SpawnAtPosition(MobHuman, map.GridCoords);
            var chestArmoredHuman = entMan.SpawnAtPosition(MobHuman, map.GridCoords);
            var unspecifiedArmoredHuman = entMan.SpawnAtPosition(MobHuman, map.GridCoords);

            var headVest = entMan.SpawnEntity(ArmorVest, map.MapCoords);
            var chestVest = entMan.SpawnEntity(ArmorVest, map.MapCoords);
            var unspecifiedVest = entMan.SpawnEntity(ArmorVest, map.MapCoords);

            Assert.That(invSys.TryEquip(headArmoredHuman, headVest, "outerClothing"), Is.True);
            Assert.That(invSys.TryEquip(chestArmoredHuman, chestVest, "outerClothing"), Is.True);
            Assert.That(invSys.TryEquip(unspecifiedArmoredHuman, unspecifiedVest, "outerClothing"), Is.True);

            var damage = new DamageSpecifier { DamageDict = { ["Blunt"] = FixedPoint2.New(10) } };

            var headUnarmored = damageSys.ChangeDamage(unarmored, damage, targetPart: TargetBodyPart.Head).GetTotal();
            var chestUnarmored = damageSys.ChangeDamage(unarmored, damage, targetPart: TargetBodyPart.Chest).GetTotal();

            var headArmored = damageSys.ChangeDamage(headArmoredHuman, damage, targetPart: TargetBodyPart.Head).GetTotal();
            var chestArmored = damageSys.ChangeDamage(chestArmoredHuman, damage, targetPart: TargetBodyPart.Chest).GetTotal();
            var unspecifiedArmored = damageSys.ChangeDamage(unspecifiedArmoredHuman, damage).GetTotal();

            Assert.That(headUnarmored, Is.EqualTo(FixedPoint2.New(10)));
            Assert.That(chestUnarmored, Is.EqualTo(FixedPoint2.New(10)));

            Assert.That(headArmored, Is.EqualTo(FixedPoint2.New(10)),
                "Chest-only armor must not reduce head damage.");
            Assert.That(chestArmored, Is.EqualTo(FixedPoint2.New(7)),
                "Chest armor should apply its blunt coefficient (0.7) to chest hits.");
            Assert.That(unspecifiedArmored, Is.EqualTo(FixedPoint2.New(7)),
                "Hits without targetPart must still stack armor before wound routing.");

            Assert.That(woundSys.GetBodySeverityPoint(chestArmoredHuman), Is.EqualTo(FixedPoint2.New(7)),
                "Wound severity on consciousness mobs must reflect armor-reduced damage.");
            Assert.That(woundSys.GetBodySeverityPoint(unspecifiedArmoredHuman), Is.EqualTo(FixedPoint2.New(7)),
                "Wound severity must reflect armor even when hit location is chosen during routing.");
        });
    }
}
