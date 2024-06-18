﻿#nullable enable
using System.Linq;
using Content.Server.Backmen.SpecForces;
using Content.Server.Body.Components;
using Content.Server.GameTicking;
using Content.Server.Ghost.Roles;
using Content.Server.Ghost.Roles.Components;
using Content.Shared.Backmen.CCVar;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Ghost;
using Content.Shared.Inventory;
using Content.Shared.Mind;
using Content.Shared.Players;
using Robust.Shared.Console;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Backmen.Specforce;

[TestFixture]
public sealed class SpecForceTest
{
    [Test]
    public async Task CallSpecForces()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Dirty = true,
            DummyTicker = false,
            Connected = true
        });

        var server = pair.Server;
        var client = pair.Client;

        var protoManager = server.ResolveDependency<IPrototypeManager>();
        var entSysManager = server.ResolveDependency<IEntitySystemManager>();
        var entMan = server.EntMan;
        var conHost = client.ResolveDependency<IConsoleHost>();
        var specForceSystem = entSysManager.GetEntitySystem<SpecForcesSystem>();
        var invSys = server.System<InventorySystem>();
        var ticker = server.System<GameTicker>();

        var sPlayerMan = server.ResolveDependency<Robust.Server.Player.IPlayerManager>();
        var session = sPlayerMan.Sessions.Single();

        // Set SpecForce cooldown to 0
        await server.WaitPost(()=>server.CfgMan.SetCVar(CCVars.SpecForceDelay, 0));

        // The game should be running for CallOps to work properly
        Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.InRound));

        // Try to spawn every SpecForceTeam
        foreach (var teamProto in protoManager.EnumeratePrototypes<SpecForceTeamPrototype>())
        {
            await server.WaitPost(() => specForceSystem.Log.Info($"Calling {teamProto.ID} SpecForce team!"));
            await server.WaitPost(() =>
            {
                // Call every specForce and force spawn every extra specforce from the SpecForceSpawn prototype.
                // This way it is spawning EVERY available ghost role, so we can also check them.
                if (!specForceSystem.CallOps(teamProto, "Test", teamProto.SpecForceSpawn.Count))
                    Assert.Fail($"CallOps method failed while trying to spawn {teamProto.ID} SpecForce.");
            });

            // Now check if there are any GhostRoles and SpecForces
            Assert.That(entMan.Count<GhostRoleComponent>(), Is.GreaterThan(0));
            Assert.That(entMan.Count<SpecForceComponent>(), Is.GreaterThan(0));
        }

        // Get all ghost roles and take them over.
        var ghostRoles = entMan.EntityQuery<GhostRoleComponent>().ToList();
        foreach (var ghostRoleComp in ghostRoles)
        {
            // Take the ghost role.
            await server.WaitPost(() =>
            {
                var id = entMan.GetComponent<GhostRoleComponent>(ghostRoleComp.Owner).Identifier;
                entMan.EntitySysManager.GetEntitySystem<GhostRoleSystem>().Takeover(session, id);
            });

            // Check that role name and description is valid.
            // We must wait because GhostRoleComponent uses Localisation methods in get property
            await server.WaitPost(() =>
            {
                Assert.That(ghostRoleComp.RoleName, Is.Not.EqualTo("Unknown"));
                Assert.That(ghostRoleComp.RoleDescription, Is.Not.EqualTo("Unknown"));
            });

            // Check player got attached to ghost role.
            var player = session.AttachedEntity!.Value;
            await pair.RunTicksSync(10);
            var newMindId = session.ContentData()!.Mind!.Value;
            var newMind = entMan.GetComponent<MindComponent>(newMindId);
            Assert.That(newMind.OwnedEntity, Is.EqualTo(player));
            Assert.That(newMind.VisitingEntity, Is.Null);
            Assert.That(entMan.HasComponent<GhostComponent>(player),
                Is.False,
                $"Player {entMan.ToPrettyString(player)} is still a ghost after attaching to an entity!");

            await server.WaitPost(() =>
                specForceSystem.Log.Info("Player attaching succeeded, starting side checks."));

            // SpecForce should have at least 3 items in their inventory slots.
            var enumerator = invSys.GetSlotEnumerator(player);
            var total = 0;
            while (enumerator.NextItem(out _))
            {
                total++;
            }

            Assert.That(total,
                Is.GreaterThan(3),
                $"SpecForce {entMan.ToPrettyString(player)} has less than 3 items in inventory: {total}.");

            // Finally check if The Great NT Evil-Fighter Agent passed basic training and figured out how to breathe.
            await pair.RunTicksSync(10);
            var totalSeconds = 30;
            var totalTicks = (int) Math.Ceiling(totalSeconds / server.Timing.TickPeriod.TotalSeconds);
            int increment = 5;
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

            // Use the ghost command at the end and move on
            conHost.ExecuteCommand("ghost");
            await pair.RunTicksSync(5);
        }

        await pair.CleanReturnAsync();
    }
}
