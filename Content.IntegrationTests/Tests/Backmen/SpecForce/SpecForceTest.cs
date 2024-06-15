using Content.Server.Backmen.SpecForces;
using Content.Server.GameTicking;
using Content.Server.Ghost.Roles.Components;
using Content.Shared.Backmen.CCVar;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Backmen.Specforce;

[TestFixture]
[TestOf(typeof(SpecForcesSystem))]
public sealed class SpecForceTest
{
    [Test]
    public async Task CallSpecForces()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var protoManager = server.ResolveDependency<IPrototypeManager>();
        var entSysManager = server.ResolveDependency<IEntitySystemManager>();
        var entMan = server.EntMan;
        var specForceSystem = entSysManager.GetEntitySystem<SpecForcesSystem>();
        var ticker = server.System<GameTicker>();

        // Set SpecForce cooldown to 0
        await server.WaitPost(()=>server.CfgMan.SetCVar(CCVars.SpecForceDelay, 0));

        // Game should have started
        Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.InRound));

        // Try to spawn every SpecForceTeam
        foreach (var teamProto in protoManager.EnumeratePrototypes<SpecForceTeamPrototype>())
        {
            // Here it probably can fail only because the shuttle didn't spawn
            if (!specForceSystem.CallOps(teamProto))
                Assert.Fail();

            // Now check if there are any GhostRoles and SpecForces
            Assert.That(entMan.Count<GhostRoleComponent>(), Is.GreaterThan(0));
            Assert.That(entMan.Count<SpecForceComponent>(), Is.GreaterThan(0));

            // TODO: Probably need to implement more detailed check in the future, like does the spawned roles have any gear.
        }

        await pair.CleanReturnAsync();
    }
}
