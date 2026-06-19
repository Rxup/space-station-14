using Content.IntegrationTests.Fixtures;
using Content.Server.Backmen.Shipwrecked.Components;
using Content.Server.Humanoid.Systems;
using Content.Shared.Backmen.Surgery.Consciousness.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Trigger;
using Content.Shared.Trigger.Components.Triggers;
using Content.Shared.Zombies;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Backmen.Shipwreck;

/// <summary>
/// Salvage corpses and shipwreck "zombie surprise" NPCs should spawn dead.
/// Surprise zombies only turn into zombies after their proximity trigger fires.
/// </summary>
[TestFixture]
public sealed class ShipwreckCorpseTest : GameTest
{
    private static readonly EntProtoId SalvageHumanCorpse = "SalvageHumanCorpse";

    public override PoolSettings PoolSettings => new()
    {
        Connected = true,
        Dirty = true,
    };

    [Test]
    public async Task SalvageHumanCorpse_SpawnsDead()
    {
        var map = await Pair.CreateTestMap();
        EntityUid corpse = default;

        await Server.WaitPost(() =>
        {
            corpse = Server.EntMan.Spawn(SalvageHumanCorpse, map.MapCoords);
        });

        await Pair.RunTicksSync(5);

        await Server.WaitAssertion(() =>
        {
            Assert.That(Server.EntMan.EntityExists(corpse), Is.True);
            Assert.That(Server.EntMan.TryGetComponent(corpse, out MobStateComponent? mobState), Is.True);
            Assert.That(mobState!.CurrentState, Is.EqualTo(MobState.Dead),
                "Unidentified salvage corpses must be dead immediately on spawn.");
        });
    }

    [Test]
    public async Task ZombieSurprise_SpawnsDeadWithDetector()
    {
        var map = await Pair.CreateTestMap();
        var randomHumanoid = Server.EntMan.System<RandomHumanoidSystem>();
        EntityUid corpse = default;
        EntityUid? detector = null;

        await Server.WaitPost(() =>
        {
            corpse = randomHumanoid.SpawnRandomHumanoid("ZombieSurprise", map.GridCoords, "zombie surprise");
        });

        await Pair.RunTicksSync(5);

        await Server.WaitAssertion(() =>
        {
            Assert.That(Server.EntMan.EntityExists(corpse), Is.True);
            Assert.That(Server.EntMan.TryGetComponent(corpse, out MobStateComponent? mobState), Is.True);
            Assert.That(mobState!.CurrentState, Is.EqualTo(MobState.Dead),
                "Zombie surprise NPCs should pose as corpses until triggered.");

            Assert.That(Server.EntMan.HasComponent<ZombieComponent>(corpse), Is.False,
                "Zombie surprise must not be zombified on spawn.");
            Assert.That(Server.EntMan.HasComponent<ZombieSurpriseComponent>(corpse), Is.True);

            var query = Server.EntMan.AllEntityQueryEnumerator<ZombieWakeupOnTriggerComponent, TriggerOnProximityComponent>();
            while (query.MoveNext(out var uid, out var wakeup, out _))
            {
                if (wakeup.ToZombify != corpse)
                    continue;

                detector = uid;
                break;
            }

            Assert.That(detector, Is.Not.Null, "Zombie surprise should spawn a proximity detector.");
        });
    }

    [Test]
    public async Task ZombieSurprise_ProximityTrigger_Zombifies()
    {
        var map = await Pair.CreateTestMap();
        var randomHumanoid = Server.EntMan.System<RandomHumanoidSystem>();
        EntityUid corpse = default;
        EntityUid detector = default;

        await Server.WaitPost(() =>
        {
            corpse = randomHumanoid.SpawnRandomHumanoid("ZombieSurprise", map.GridCoords, "zombie surprise");
        });

        await Pair.RunTicksSync(5);

        await Server.WaitPost(() =>
        {
            var query = Server.EntMan.AllEntityQueryEnumerator<ZombieWakeupOnTriggerComponent>();
            while (query.MoveNext(out var uid, out var wakeup))
            {
                if (wakeup.ToZombify != corpse)
                    continue;

                detector = uid;
                break;
            }

            Assert.That(detector, Is.Not.EqualTo(EntityUid.Invalid), "Expected a zombie surprise detector.");
            var triggerEv = new TriggerEvent(corpse);
            Server.EntMan.EventBus.RaiseLocalEvent(detector, ref triggerEv);
        });

        await Pair.RunTicksSync(10);

        await Server.WaitAssertion(() =>
        {
            Assert.That(Server.EntMan.EntityExists(corpse), Is.True);
            Assert.That(Server.EntMan.HasComponent<ZombieComponent>(corpse), Is.True,
                "Zombie surprise should become a zombie after its trigger fires.");
            Assert.That(Server.EntMan.HasComponent<ConsciousnessComponent>(corpse), Is.False,
                "Zombification removes consciousness from surprise zombies.");

            Assert.That(Server.EntMan.TryGetComponent(corpse, out MobStateComponent? mobState), Is.True);
            Assert.That(mobState!.CurrentState, Is.EqualTo(MobState.Alive),
                "Zombified surprise NPC should be alive.");
        });
    }
}
