using Content.IntegrationTests.Fixtures;
using Content.Shared.Backmen.Body.Systems;
using Content.Shared.Body;
using Content.Shared.Body.Part;
using Content.Shared.Inventory;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.Backmen.Body;

/// <summary>
/// Slot visibility is server state. A client that runs organ-insert sync during spawn used to hide
/// slots locally; leaving PVS was the only way the real (enabled) state came back.
/// </summary>
[TestFixture]
public sealed class SpawnedInventorySlotsTest : GameTest
{
    private static readonly string[] ClothingSlots =
    [
        "eyes",
        "ears",
        "head",
        "mask",
        "neck",
        "gloves",
        "shoes",
        "socks",
    ];

    [Test]
    public async Task SpawnedHuman_ClientKeepsSlotsUntilServerDisables()
    {
        var map = await Pair.CreateTestMap();
        EntityUid human = default;

        await Server.WaitAssertion(() => human = Server.EntMan.Spawn("MobHuman", map.MapCoords));
        await Pair.RunTicksSync(5);

        var clientHuman = ToClientUid(human);
        var clientInv = Client.EntMan.System<InventorySystem>();
        var clientBody = Client.EntMan.System<BkmBodySharedSystem>();

        await Client.WaitAssertion(() =>
        {
            AssertSlotsEnabled(clientInv, clientHuman);

            // Partial organ replication used to take this path and hide the slots.
            clientBody.SyncInventorySlotsForPartType(clientHuman, BodyPartType.Head);
            clientBody.SyncInventorySlotsForPartType(clientHuman, BodyPartType.Hand);
            clientBody.SyncInventorySlotsForPartType(clientHuman, BodyPartType.Foot);
            AssertSlotsEnabled(clientInv, clientHuman);
        });

        await Server.WaitAssertion(() =>
        {
            var body = Server.EntMan.System<BkmBodySharedSystem>();
            var organs = Server.EntMan.System<BodySystem>();
            Assert.That(organs.TryGetOrganByCategory(human, "HandLeft", out var left), Is.True);
            Assert.That(organs.TryGetOrganByCategory(human, "HandRight", out var right), Is.True);
            Assert.That(body.RemoveOrgan(left), Is.True);
            Assert.That(body.RemoveOrgan(right), Is.True);
        });

        await Pair.RunTicksSync(5);

        await Client.WaitAssertion(() =>
        {
            Assert.That(clientInv.IsSlotDisabled(clientHuman, "gloves"), Is.True);
            Assert.That(clientInv.IsSlotDisabled(clientHuman, "eyes"), Is.False);
            Assert.That(clientInv.IsSlotDisabled(clientHuman, "shoes"), Is.False);
        });

        await Server.WaitPost(() => Server.EntMan.DeleteEntity(map.MapUid));
    }

    private static void AssertSlotsEnabled(InventorySystem inv, EntityUid body)
    {
        foreach (var slot in ClothingSlots)
        {
            Assert.That(inv.IsSlotDisabled(body, slot), Is.False, $"slot {slot} disabled on client after spawn");
            Assert.That(inv.TryGetSlot(body, slot, out var def), Is.True, $"slot {slot} missing on client after spawn");
            Assert.That(def!.Disabled, Is.False, $"slot {slot} marked disabled on client after spawn");
        }
    }
}
