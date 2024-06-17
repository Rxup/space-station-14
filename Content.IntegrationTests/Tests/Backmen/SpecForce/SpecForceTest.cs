#nullable enable
using System.Linq;
using Content.Server.Backmen.SpecForces;
using Content.Server.Body.Components;
using Content.Server.GameTicking;
using Content.Server.Ghost.Roles;
using Content.Server.Ghost.Roles.Components;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Inventory;
using Content.Shared.Mind;
using Content.Shared.Players;
using Robust.Shared.Console;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using CCVars = Content.Shared.CCVar.CCVars;
using BkmCCVars = Content.Shared.Backmen.CCVar.CCVars;

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
        var originalMindId = session.ContentData()!.Mind!.Value;

        // Set SpecForce cooldown to 0
        await server.WaitPost(()=>server.CfgMan.SetCVar(BkmCCVars.SpecForceDelay, 0));
        await server.WaitPost(()=>server.CfgMan.SetCVar(CCVars.GridFill, true));

        // The game should be running for CallOps to work properly
        Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.InRound));

        // Try to spawn every SpecForceTeam
        foreach (var teamProto in protoManager.EnumeratePrototypes<SpecForceTeamPrototype>())
        {
            await server.WaitPost(() =>
            {
                // Here it probably can fail only because the shuttle didn't spawn
                if (!specForceSystem.CallOps(teamProto))
                    Assert.Fail($"CallOps method failed while trying to spawn {teamProto.ID} SpecForce.");
            });

            // Now check if there are any GhostRoles and SpecForces
            Assert.That(entMan.Count<GhostRoleComponent>(), Is.GreaterThan(0));
            Assert.That(entMan.Count<SpecForceComponent>(), Is.GreaterThan(0));

            // Get all ghost roles and take them over.
            var ghostRoles = entMan.EntityQuery<GhostRoleComponent>();
            foreach (var ghostRoleComp in ghostRoles)
            {
                var player = ghostRoleComp.Owner;

                // Take the ghost role
                await server.WaitPost(() =>
                {
                    var id = entMan.GetComponent<GhostRoleComponent>(player).Identifier;
                    if (!entMan.EntitySysManager.GetEntitySystem<GhostRoleSystem>().Takeover(session, id))
                        Assert.Fail("Failed attaching player to an entity.");
                });

                // Check that role name and description is valid.
                // We must wait because GhostRoleComponent uses Localisation methods in get property
                await server.WaitPost(() =>
                {
                    Assert.That(ghostRoleComp.RoleName, Is.Not.EqualTo("Unknown"));
                    Assert.That(ghostRoleComp.RoleDescription, Is.Not.EqualTo("Unknown"));
                });

                // Check player got attached to ghost role.
                await pair.RunTicksSync(10);
                var newMindId = session.ContentData()!.Mind!.Value;
                var newMind = entMan.GetComponent<MindComponent>(newMindId);
                Assert.That(newMindId, Is.Not.EqualTo(originalMindId));
                Assert.That(session.AttachedEntity, Is.EqualTo(player));
                Assert.That(newMind.OwnedEntity, Is.EqualTo(player));
                Assert.That(newMind.VisitingEntity, Is.Null);

                // SpecForce should have at least 3 items in their inventory slots.
                var enumerator = invSys.GetSlotEnumerator(player);
                var total = 0;
                while (enumerator.NextItem(out _))
                {
                    total++;
                }
                Assert.That(total, Is.GreaterThan(3));

                // Finally check if The Great NT Evil-Fighter Agent passed basic training and figured out how to breathe.
                var totalSeconds = 30;
                var totalTicks = (int) Math.Ceiling(totalSeconds / server.Timing.TickPeriod.TotalSeconds);
                int increment = 5;
                var resp = entMan.GetComponent<RespiratorComponent>(player);
                var damage = entMan.GetComponent<DamageableComponent>(player);
                for (var tick = 0; tick < totalTicks; tick += increment)
                {
                    await pair.RunTicksSync(increment);
                    Assert.That(resp.SuffocationCycles, Is.LessThanOrEqualTo(resp.SuffocationCycleThreshold));
                    Assert.That(damage.TotalDamage, Is.EqualTo(FixedPoint2.Zero));
                }

                // Use the ghost command at the end and move on
                conHost.ExecuteCommand("ghost");
                await pair.RunTicksSync(5);
            }
        }

        server.CfgMan.SetCVar(CCVars.GridFill, false);
        await pair.CleanReturnAsync();
    }
}
