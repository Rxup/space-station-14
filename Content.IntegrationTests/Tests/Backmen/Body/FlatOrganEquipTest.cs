using Content.IntegrationTests.Fixtures;
using Content.Shared.Backmen.Body.Systems;
using Content.Shared.Body.Part;
using Content.Shared.Inventory;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.Backmen.Body;

[TestFixture]
public sealed class FlatOrganEquipTest : GameTest
{
    public override PoolSettings PoolSettings => new()
    {
        Dirty = true,
        Connected = false,
        InLobby = false,
    };

    [Test]
    public async Task FlatOrganMob_CanEquipHeadMappedSlotsWithoutHeadOrgan()
    {
        var entMan = Server.ResolveDependency<IEntityManager>();
        var bodySystem = entMan.System<BkmBodySharedSystem>();
        var invSystem = entMan.System<InventorySystem>();

        var testMap = await Pair.CreateTestMap();

        EntityUid parrot = default;
        EntityUid headset = default;

        await Server.WaitAssertion(() =>
        {
            parrot = entMan.Spawn("MobParrot", testMap.MapCoords);
            headset = entMan.Spawn("ClothingHeadsetEngineering", testMap.MapCoords);
        });

        await Server.WaitIdleAsync();
        await Server.WaitRunTicks(2);

        await Server.WaitAssertion(() =>
        {
            Assert.That(bodySystem.UsesFlatOrgans(parrot), Is.True);
            Assert.That(bodySystem.GetBodyPartCount(parrot, BodyPartType.Head), Is.Zero);
            Assert.That(invSystem.CanEquip(parrot, headset, "ears", out _), Is.True);
            Assert.That(invSystem.TryEquip(parrot, headset, "ears"), Is.True);
        });
    }
}
