using Content.IntegrationTests.Fixtures;
using Content.Shared.Backmen.Body.Systems;
using Content.Shared.Body;
using Content.Shared.Inventory;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.Backmen.Body;

[TestFixture]
public sealed class LeglessFootwearEquipTest : GameTest
{
    public override PoolSettings PoolSettings => new()
    {
        Dirty = true,
        Connected = false,
        InLobby = false,
    };

    [Test]
    public async Task HumanWithoutLegs_CannotEquipShoes()
    {
        var entMan = Server.ResolveDependency<IEntityManager>();
        var bodySystem = entMan.System<BkmBodySharedSystem>();
        var organBody = entMan.System<BodySystem>();
        var invSystem = entMan.System<InventorySystem>();

        var testMap = await Pair.CreateTestMap();

        EntityUid human = default;
        EntityUid shoes = default;

        await Server.WaitAssertion(() =>
        {
            human = entMan.Spawn("MobHuman", testMap.MapCoords);
            shoes = entMan.Spawn("ClothingShoesColorBlack", testMap.MapCoords);
        });

        await Server.WaitIdleAsync();
        await Server.WaitRunTicks(2);

        await Server.WaitAssertion(() =>
        {
            Assert.That(bodySystem.CanWearFootwear(human), Is.True);
            Assert.That(invSystem.CanEquip(human, shoes, "shoes", out _), Is.True);
            Assert.That(invSystem.TryGetSlot(human, "shoes", out var shoesSlot), Is.True);
            Assert.That(shoesSlot!.Disabled, Is.False);

            Assert.That(organBody.TryGetOrganByCategory(human, "LegLeft", out var leftLeg), Is.True);
            Assert.That(organBody.TryGetOrganByCategory(human, "LegRight", out var rightLeg), Is.True);
            Assert.That(bodySystem.RemoveOrgan(leftLeg), Is.True);
            Assert.That(bodySystem.RemoveOrgan(rightLeg), Is.True);

            Assert.That(bodySystem.CanWearFootwear(human), Is.False);
            Assert.That(invSystem.CanEquip(human, shoes, "shoes", out _), Is.False);
            Assert.That(invSystem.TryEquip(human, shoes, "shoes"), Is.False);
            Assert.That(invSystem.TryGetSlot(human, "shoes", out _), Is.False);
            Assert.That(invSystem.IsSlotDisabled(human, "shoes"), Is.True);
            Assert.That(invSystem.IsSlotDisabled(human, "socks"), Is.True);
        });
    }
}
