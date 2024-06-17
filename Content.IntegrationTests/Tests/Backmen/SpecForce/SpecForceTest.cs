#nullable enable
using System.Linq;
using Content.Server.Backmen.SpecForces;
using Content.Server.Body.Components;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Presets;
using Content.Server.Ghost.Roles;
using Content.Server.Ghost.Roles.Components;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.GameTicking;
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
            Connected = true,
            InLobby = true
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

        // Set SpecForce cooldown to 0
        await server.WaitPost(()=>server.CfgMan.SetCVar(BkmCCVars.SpecForceDelay, 0));
        await server.WaitPost(()=>server.CfgMan.SetCVar(CCVars.GridFill, true));

        // Initially in the lobby
        Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.PreRoundLobby));
        Assert.That(client.AttachedEntity, Is.Null);
        Assert.That(ticker.PlayerGameStatuses[client.User!.Value], Is.EqualTo(PlayerGameStatus.NotReadyToPlay));

        // Add several dummy players
        await pair.Server.AddDummySessions(1);
        await pair.RunTicksSync(5);

        var sPlayerMan = server.ResolveDependency<Robust.Server.Player.IPlayerManager>();
        var session = sPlayerMan.Sessions.Last();
        var originalMindId = session.ContentData()!.Mind!.Value;

        // Start normal round
        ticker.ToggleReadyAll(true);
        Assert.That(ticker.PlayerGameStatuses.Values.All(x => x == PlayerGameStatus.ReadyToPlay));
        await pair.WaitCommand("setgamepreset extended");
        await pair.WaitCommand("startround");
        await pair.RunTicksSync(10);

        // Game should have started
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

                // Check that role name and description is valid
                Assert.That(ghostRoleComp.RoleName, Is.Not.EqualTo("Unknown"));
                Assert.That(ghostRoleComp.RoleDescription, Is.Not.EqualTo("Unknown"));

                // Take the ghost role
                await server.WaitPost(() =>
                {
                    var id = ghostRoleComp.Identifier;
                    entMan.EntitySysManager.GetEntitySystem<GhostRoleSystem>().Takeover(session, id);
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

        ticker.SetGamePreset((GamePresetPrototype?)null);
        server.CfgMan.SetCVar(CCVars.GridFill, false);
        server.CfgMan.SetCVar(CCVars.GameLobbyFallbackEnabled, true);
        server.CfgMan.SetCVar(CCVars.GameLobbyDefaultPreset, "secret");

        await pair.CleanReturnAsync();
    }
}
