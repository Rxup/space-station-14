using System.Collections.Generic;
using System.Linq;
using Content.Server.Backmen.Body.Systems;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Body.Organ;
using Content.Shared.Backmen.Body.OrganRelations;
using Content.Shared.Backmen.Body.Systems;
using Content.Shared.Body;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Backmen.Body;

/// <summary>
/// Supermatter dusting respects flat vs layered bodies and preserves brains.
/// </summary>
[TestFixture]
public sealed class SupermatterDustTest : GameTest
{
    private static readonly EntProtoId Supermatter = "supermatter";
    private static readonly EntProtoId Ash = "Ash";

    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [Test]
    public async Task HumanTouchingSupermatter_IsRemovedAndBrainPreserved()
    {
        var map = await Pair.CreateTestMap();
        NetEntity netMob = default;
        NetEntity netBrain = default;

        await Server.WaitAssertion(() =>
        {
            var entMan = Server.EntMan;
            var organBody = entMan.System<BodySystem>();

            var sm = entMan.SpawnEntity(Supermatter, map.MapCoords);
            var human = entMan.SpawnEntity("MobHuman", map.MapCoords);
            Assert.That(organBody.TryGetOrganByCategory(human, "Brain", out var brain), Is.True);
            netMob = entMan.GetNetEntity(human);
            netBrain = entMan.GetNetEntity(brain);

            entMan.EventBus.RaiseLocalEvent(sm, new InteractHandEvent(human, sm));
        });

        await Server.WaitIdleAsync();
        await Server.WaitRunTicks(5);

        await Server.WaitAssertion(() =>
        {
            var entMan = Server.EntMan;
            var mob = entMan.GetEntity(netMob);
            var brain = entMan.GetEntity(netBrain);

            Assert.That(entMan.EntityExists(mob), Is.False, "Human should be dusted by supermatter.");
            Assert.That(entMan.EntityExists(brain), Is.True, "Brain should be preserved.");
            Assert.That(entMan.HasComponent<BkmDetachedBrainProtectionComponent>(brain), Is.True);
        });
    }

    [Test]
    public async Task CarpTouchingSupermatter_SpawnsAshAndDeletesMob()
    {
        var map = await Pair.CreateTestMap();
        NetEntity netCarp = default;

        await Server.WaitAssertion(() =>
        {
            var entMan = Server.EntMan;
            var sm = entMan.SpawnEntity(Supermatter, map.MapCoords);
            var carp = entMan.SpawnEntity("MobCarp", map.MapCoords);
            netCarp = entMan.GetNetEntity(carp);

            entMan.EventBus.RaiseLocalEvent(sm, new InteractHandEvent(carp, sm));
        });

        await Server.WaitIdleAsync();

        await Server.WaitAssertion(() =>
        {
            var entMan = Server.EntMan;
            var carp = entMan.GetEntity(netCarp);

            Assert.That(entMan.EntityExists(carp), Is.False, "Flat carp should be deleted.");
            Assert.That(CountPrototype(entMan, Ash), Is.GreaterThan(0), "Carp should leave ash.");
        });
    }

    [Test]
    public async Task ItemTouchingSupermatter_SpawnsAshAndDeletes()
    {
        var map = await Pair.CreateTestMap();
        NetEntity netItem = default;

        await Server.WaitAssertion(() =>
        {
            var entMan = Server.EntMan;
            var burnBody = entMan.System<BkmBurnBodySystem>();
            var sm = entMan.SpawnEntity(Supermatter, map.MapCoords);
            var item = entMan.SpawnEntity("Crowbar", map.MapCoords);
            netItem = entMan.GetNetEntity(item);

            if (!burnBody.TryDustEntity(item, sm))
                burnBody.DustFlatEntity(item, sm);
        });

        await Server.WaitIdleAsync();
        await Server.WaitRunTicks(5);

        await Server.WaitAssertion(() =>
        {
            var entMan = Server.EntMan;
            var item = entMan.GetEntity(netItem);

            Assert.That(entMan.EntityExists(item), Is.False, "Item should be deleted.");
            Assert.That(CountPrototype(entMan, Ash), Is.GreaterThan(0), "Item should leave ash.");
        });
    }

    [Test]
    public async Task BrainTouchingSupermatter_IsPreservedNotDeleted()
    {
        var map = await Pair.CreateTestMap();
        NetEntity netBrain = default;

        await Server.WaitAssertion(() =>
        {
            var entMan = Server.EntMan;
            var burnBody = entMan.System<BkmBurnBodySystem>();
            var sm = entMan.SpawnEntity(Supermatter, map.MapCoords);
            var brain = entMan.SpawnEntity("OrganHumanBrain", map.MapCoords);
            netBrain = entMan.GetNetEntity(brain);

            Assert.That(burnBody.TryDustEntity(brain, sm), Is.True);
        });

        await Server.WaitIdleAsync();

        await Server.WaitAssertion(() =>
        {
            var entMan = Server.EntMan;
            var brain = entMan.GetEntity(netBrain);

            Assert.That(entMan.EntityExists(brain), Is.True, "Brain should not be deleted by supermatter.");
            Assert.That(entMan.HasComponent<BkmDetachedBrainProtectionComponent>(brain), Is.True);
            Assert.That(entMan.HasComponent<BrainComponent>(brain), Is.True);
        });
    }

    private static int CountPrototype(IEntityManager entMan, EntProtoId proto)
    {
        var count = 0;
        var query = entMan.AllEntityQueryEnumerator<MetaDataComponent>();
        while (query.MoveNext(out _, out var meta))
        {
            if (meta.EntityPrototype?.ID == proto)
                count++;
        }

        return count;
    }
}
