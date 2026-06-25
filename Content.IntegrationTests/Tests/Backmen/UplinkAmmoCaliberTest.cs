using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Containers;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.EntityTable;
using Content.Shared.Storage.Components;
using Content.Shared.Store;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Backmen;

[TestFixture]
public sealed class UplinkAmmoCaliberTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false };

    private static readonly ProtoId<StoreCategoryPrototype> UplinkAmmoCategory = "UplinkAmmo";

    /// <summary>
    /// Vanilla .35 auto ammunition prototypes that must not appear in the uplink.
    /// </summary>
    private static readonly HashSet<string> Vanilla35AmmoProducts =
    [
        "MagazinePistol",
        "MagazinePistolEmpty",
        "MagazinePistolPractice",
        "MagazinePistolIncendiary",
        "MagazinePistolUranium",
        "MagazinePistolSubMachineGun",
        "MagazinePistolSubMachineGunEmpty",
        "MagazinePistolSubMachineGunPractice",
        "MagazinePistolSubMachineGunUranium",
        "MagazinePistolSubMachineGunIncendiary",
        "MagazinePistolHighCapacity",
        "MagazinePistolHighCapacityEmpty",
        "MagazinePistolHighCapacityPractice",
        "MagazineBoxPistol",
        "MagazineBoxPistolPractice",
        "MagazineBoxPistolIncendiary",
        "MagazineBoxPistolUranium",
        "SpeedLoaderPistol",
        "SpeedLoaderPistolPractice",
        "CartridgePistol",
        "CartridgePistolPractice",
        "CartridgePistolIncendiary",
        "CartridgePistolUranium",
    ];

    private static readonly Dictionary<string, string> RequiredUplinkAmmoProducts = new()
    {
        ["UplinkPistol9mmMagazine"] = "MagazinePistol9x17",
        ["UplinkMagazinePistolSubMachineGun"] = "MagazinePistolSubMachineGun9x17",
    };

    private static readonly EntProtoId Mk58 = "WeaponPistolMk58";
    private static readonly EntProtoId C20r = "WeaponSubMachineGunC20r";
    private static readonly EntProtoId Drozd = "WeaponSubMachineGunDrozd";
    private static readonly EntProtoId C20rBundle = "ClothingBackpackDuffelSyndicateFilledSMG";
    private static readonly EntProtoId NukieAmmoBundle = "ClothingBackpackDuffelSyndicateAmmoBackmenFilled";
    private static readonly EntProtoId PistolMag9x17 = "MagazinePistol9x17";
    private static readonly EntProtoId SmgMag9x17 = "MagazinePistolSubMachineGun9x17";

    [Test]
    public async Task UplinkAmmo_UsesBackmenCalibers()
    {
        var protoMan = Server.ProtoMan;
        var compFact = Server.ResolveDependency<IComponentFactory>();
        var entityTable = Server.EntMan.System<EntityTableSystem>();

        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                foreach (var (listingId, expectedProduct) in RequiredUplinkAmmoProducts)
                {
                    Assert.That(protoMan.TryIndex<ListingPrototype>(listingId, out var listing), Is.True,
                        $"Missing uplink listing {listingId}.");
                    Assert.That(listing.ProductEntity?.ToString(), Is.EqualTo(expectedProduct),
                        $"Listing {listingId} should sell {expectedProduct}.");
                }

                foreach (var listing in protoMan.EnumeratePrototypes<ListingPrototype>())
                {
                    if (!listing.Categories.Contains(UplinkAmmoCategory) || listing.ProductEntity == null)
                        continue;

                    var productId = listing.ProductEntity.Value.ToString();
                    Assert.That(Vanilla35AmmoProducts, Does.Not.Contain(productId),
                        $"Uplink listing {listing.ID} still sells vanilla .35 product {productId}.");

                    if (!protoMan.TryIndex(listing.ProductEntity.Value, out var productProto))
                        continue;

                    AssertUsesBackmenAmmo(productProto, listing.ID, compFact, entityTable);
                }

                AssertWeaponUses9x17Pistol(protoMan.Index(Mk58), compFact);
                AssertWeaponUses9x17Smg(protoMan.Index(C20r), compFact);
                AssertWeaponUses9x17Smg(protoMan.Index(Drozd), compFact);

                AssertBundleContains(protoMan.Index(C20rBundle), SmgMag9x17, compFact, entityTable);
                AssertBundleContains(protoMan.Index(NukieAmmoBundle), PistolMag9x17, compFact, entityTable);
                AssertBundleContains(protoMan.Index(NukieAmmoBundle), SmgMag9x17, compFact, entityTable);
            });
        });
    }

    private static void AssertUsesBackmenAmmo(
        EntityPrototype productProto,
        string listingId,
        IComponentFactory compFact,
        EntityTableSystem entityTable)
    {
        if (productProto.TryGetComponent<BallisticAmmoProviderComponent>(out var ballistic, compFact))
        {
            Assert.That(ballistic.Proto?.ToString(), Is.Not.EqualTo("CartridgePistol"),
                $"Uplink listing {listingId} product {productProto.ID} still uses vanilla CartridgePistol.");
        }

        foreach (var spawnId in GetFillSpawnIds(productProto, compFact, entityTable))
        {
            Assert.That(Vanilla35AmmoProducts, Does.Not.Contain(spawnId.ToString()),
                $"Uplink listing {listingId} bundle {productProto.ID} contains vanilla .35 spawn {spawnId}.");
        }
    }

    private static void AssertWeaponUses9x17Pistol(EntityPrototype weapon, IComponentFactory compFact)
    {
        Assert.That(weapon.TryGetComponent<ItemSlotsComponent>(out var slots, compFact), Is.True,
            $"{weapon.ID} is missing item slots.");

        Assert.That(slots.Slots, Contains.Key("gun_magazine"));
        var magSlot = slots.Slots["gun_magazine"];
        Assert.That(magSlot.StartingItem, Is.EqualTo("MagazinePistol9x17"),
            $"{weapon.ID} should start with a 9x17 pistol magazine.");
        Assert.That(magSlot.Whitelist?.Tags, Does.Contain("MagazinePistol9x17"),
            $"{weapon.ID} should accept MagazinePistol9x17.");

        Assert.That(slots.Slots, Contains.Key("gun_chamber"));
        var chamberSlot = slots.Slots["gun_chamber"];
        Assert.That(chamberSlot.StartingItem, Is.EqualTo("CartridgePistol9x17"));
        Assert.That(chamberSlot.Whitelist?.Tags, Does.Contain("CartridgePistol9x17"));
    }

    private static void AssertWeaponUses9x17Smg(EntityPrototype weapon, IComponentFactory compFact)
    {
        Assert.That(weapon.TryGetComponent<ItemSlotsComponent>(out var slots, compFact), Is.True,
            $"{weapon.ID} is missing item slots.");

        Assert.That(slots.Slots, Contains.Key("gun_magazine"));
        var magSlot = slots.Slots["gun_magazine"];
        Assert.That(magSlot.StartingItem, Is.EqualTo("MagazinePistolSubMachineGun9x17"),
            $"{weapon.ID} should start with a 9x17 SMG magazine.");
        Assert.That(magSlot.Whitelist?.Tags, Does.Contain("MagazineSMG9x17"),
            $"{weapon.ID} should accept MagazineSMG9x17.");

        Assert.That(slots.Slots, Contains.Key("gun_chamber"));
        var chamberSlot = slots.Slots["gun_chamber"];
        Assert.That(chamberSlot.StartingItem, Is.EqualTo("CartridgePistol9x17"));
        Assert.That(chamberSlot.Whitelist?.Tags, Does.Contain("CartridgePistol9x17"));
    }

    private static void AssertBundleContains(
        EntityPrototype bundle,
        EntProtoId expectedSpawn,
        IComponentFactory compFact,
        EntityTableSystem entityTable)
    {
        var spawns = GetFillSpawnIds(bundle, compFact, entityTable)
            .Select(id => id.ToString())
            .ToHashSet();

        Assert.That(spawns, Does.Contain(expectedSpawn.ToString()),
            $"Bundle {bundle.ID} should contain {expectedSpawn}. Found: {string.Join(", ", spawns)}");
    }

    private static IEnumerable<EntProtoId> GetFillSpawnIds(
        EntityPrototype proto,
        IComponentFactory compFact,
        EntityTableSystem entityTable)
    {
        if (proto.TryGetComponent<StorageFillComponent>(out var fill, compFact))
        {
            foreach (var entry in fill.Contents)
            {
                if (entry.PrototypeId != null)
                    yield return entry.PrototypeId.Value;
            }
        }

        if (!proto.TryGetComponent<EntityTableContainerFillComponent>(out var tableFill, compFact))
            yield break;

        foreach (var selector in tableFill.Containers.Values)
        {
            foreach (var spawn in entityTable.GetSpawns(selector))
                yield return spawn;
        }
    }
}
