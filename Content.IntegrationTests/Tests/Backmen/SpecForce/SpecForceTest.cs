#nullable enable
using System.Linq;
using Content.Server.Backmen.SpecForces;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Presets;
using Content.Server.Ghost.Roles.Components;
using Content.Shared.GameTicking;
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
        var specForceSystem = entSysManager.GetEntitySystem<SpecForcesSystem>();
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
            // Here it probably can fail only because the shuttle didn't spawn
            if (!specForceSystem.CallOps(teamProto))
                Assert.Fail($"CallOps method failed while trying to spawn {teamProto.ID} SpecForce.");

            // Now check if there are any GhostRoles and SpecForces
            Assert.That(entMan.Count<GhostRoleComponent>(), Is.GreaterThan(0));
            Assert.That(entMan.Count<SpecForceComponent>(), Is.GreaterThan(0));

            // TODO: Probably need to implement more detailed check in the future, like does the spawned roles have any gear.
        }

        ticker.SetGamePreset((GamePresetPrototype?)null);
        server.CfgMan.SetCVar(CCVars.GridFill, false);
        server.CfgMan.SetCVar(CCVars.GameLobbyFallbackEnabled, true);
        server.CfgMan.SetCVar(CCVars.GameLobbyDefaultPreset, "secret");

        await pair.CleanReturnAsync();
    }
}
