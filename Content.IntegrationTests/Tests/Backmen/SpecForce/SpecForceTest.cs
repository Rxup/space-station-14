#nullable enable
using System.Linq;
using Content.Server.Backmen.SpecForces;
using Content.Server.Body.Components;
using Content.Server.GameTicking;
using Content.Server.Ghost.Roles.Components;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Inventory;
using Robust.Shared.GameObjects;
using Robust.Shared.Log;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Backmen.Specforce;

[TestFixture]
public sealed class SpecForceTest
{
    [DatapointSource]
    public static int[] Onlines = [20, 40, 70];

    [Test]
    public async Task CallSpecForces()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Dirty = false,
            DummyTicker = false,
            Connected = true
        });

        var server = pair.Server;
        var client = pair.Client;

        var protoManager = server.ResolveDependency<IPrototypeManager>();
        var entSysManager = server.ResolveDependency<IEntitySystemManager>();
        var entMan = server.EntMan;
        var specForceSystem = entSysManager.GetEntitySystem<SpecForcesSystem>();
        var invSys = server.System<InventorySystem>();
        var ticker = server.System<GameTicker>();

        var sPlayerMan = server.ResolveDependency<Robust.Server.Player.IPlayerManager>();
        var session = sPlayerMan.Sessions.Single();

        // The game should be running for CallOps to work properly
        Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.InRound));

        var doNotClean = entMan.EntityQuery<MapGridComponent>().Select(x=>x.Owner).ToArray();

        var logMan = server.ResolveDependency<ILogManager>();
        var logger = logMan.RootSawmill;

        logger.Level = specForceSystem.Log.Level = LogLevel.Verbose;

        // Try to spawn every SpecForceTeam
        foreach (var teamProto in protoManager.EnumeratePrototypes<SpecForceTeamPrototype>())
        {
            var o = teamProto.SpecForceSpawn.Count == 0 ? new[] { 1 } : Onlines;
            foreach (var online in o)
            {
                var optId = teamProto.SpecForceSpawn.Count == 0 ? 0 : specForceSystem.GetOptIdCount(teamProto, online);
                var total = teamProto.GuaranteedSpawn.Count + optId;

                total -= teamProto.GuaranteedSpawn.Count(spawnEntry => spawnEntry.SpawnProbability < 1); //GuaranteedSpawn opt-in??????? WTF?

                logger.Info($"Test online {online} in {teamProto.ID} except total {total}");

                await server.WaitPost(() =>
                {
                    logger.Info($"Calling {teamProto.ID} SpecForce team!");
                    if (!specForceSystem.CallOps(teamProto, "Test", optId == 0 ? null : optId, true))
                        Assert.Fail($"CallOps method failed while trying to spawn {teamProto.ID} SpecForce.");
                });

                // Now check if there are any GhostRoles and SpecForces
                Assert.That(entMan.Count<GhostRoleComponent>(), Is.GreaterThanOrEqualTo(total));
                Assert.That(entMan.Count<SpecForceComponent>(), Is.GreaterThanOrEqualTo(total));

                // Get all ghost roles and take them over.
                var ghostRoles = entMan.EntityQuery<GhostRoleComponent>().ToList();
                foreach (var ghostRoleComp in ghostRoles)
                {
                    if (!entMan.Deleted(ghostRoleComp.Owner) || !entMan.HasComponent<SpecForceComponent>(ghostRoleComp.Owner))
                        continue;

                    await server.WaitPost(() =>
                    {
                        Assert.That(ghostRoleComp.RoleName, Is.Not.EqualTo("Unknown"));
                        Assert.That(ghostRoleComp.RoleDescription, Is.Not.EqualTo("Unknown"));
                        // Take the ghost role.
                        var ev = new TakeGhostRoleEvent(session);
                        entMan.EventBus.RaiseLocalEvent(ghostRoleComp.Owner, ref ev);
                    });

                    await pair.ReallyBeIdle();

                    logger.Info("Player attaching succeeded, starting side checks.");

                    var player = session.AttachedEntity!.Value;

                    // SpecForce should have at least 3 items in their inventory slots.
                    var enumerator = invSys.GetSlotEnumerator(player);
                    var totalS = 0;
                    while (enumerator.NextItem(out _))
                    {
                        totalS++;
                    }

                    Assert.That(totalS,
                        Is.GreaterThan(3),
                        $"SpecForce {entMan.ToPrettyString(player)} has less than 3 items in inventory: {totalS}.");

                    // Finally check if The Great NT Evil-Fighter Agent passed basic training and figured out how to breathe.
                    await pair.RunTicksSync(10);

                    var totalSeconds = 30;
                    var totalTicks = (int) Math.Ceiling(totalSeconds / server.Timing.TickPeriod.TotalSeconds);
                    var increment = 5;
                    var resp = entMan.GetComponent<RespiratorComponent>(player);
                    var damage = entMan.GetComponent<DamageableComponent>(player);
                    for (var tick = 0; tick < totalTicks; tick += increment)
                    {
                        await pair.RunTicksSync(increment);
                        Assert.That(resp.SuffocationCycles, Is.LessThanOrEqualTo(resp.SuffocationCycleThreshold));
                        Assert.That(damage.TotalDamage,
                            Is.EqualTo(FixedPoint2.Zero),
                            $"SpecForce {entMan.ToPrettyString(player)} is stupid and don't know how to breathe!");
                    }
                    await pair.RunTicksSync(5);

                }

                logger.Info("Cleanup!");

                await server.WaitPost(() =>
                {
                    foreach (var specForceComponent in entMan.EntityQuery<SpecForceComponent>())
                    {
                        entMan.DeleteEntity(specForceComponent.Owner);
                    }
                    foreach (var map in entMan.EntityQuery<MapGridComponent>())
                    {
                        if(doNotClean.Contains(map.Owner))
                            continue;
                        entMan.DeleteEntity(map.Owner);
                    }
                });

                await pair.RunTicksSync(5);
            }
        }
        await pair.CleanReturnAsync();
    }
}
