#nullable enable
using System.Collections.Generic;
using System.Linq;
using Content.Server.Backmen.SpecForces;
using Content.Server.Ghost.Roles.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Backmen.Specforce;

[TestFixture]
public sealed class SpecForceTest
{
    /// <summary>
    /// A list of spec forces that can be ignored by this test.
    /// </summary>
    private readonly HashSet<string> _ignoredPrototypes = new() {};

    [Test]
    public async Task CallSpecForces()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Dirty = true,
            Connected = true,
            DummyTicker = false,
            InLobby = false,
        });
        var (server, client) = (pair.Server, pair.Client);

        var protoManager = server.ResolveDependency<IPrototypeManager>();
        var entSysManager = server.ResolveDependency<IEntitySystemManager>();
        var entMan = server.EntMan;
        var specForceSystem = entSysManager.GetEntitySystem<SpecForcesSystem>();

        // Try to spawn every SpecForceTeam
        await server.WaitAssertion(() =>
        {
            foreach (var teamProto in protoManager.EnumeratePrototypes<SpecForceTeamPrototype>())
            {
                if (_ignoredPrototypes.Contains(teamProto.ID))
                    continue;

                var total = teamProto.GuaranteedSpawn.Count + specForceSystem.GetOptIdCount(teamProto);
                total -= teamProto.GuaranteedSpawn.Count(spawnEntry => spawnEntry.SpawnProbability < 1);

                Assert.That(specForceSystem.CallOps(teamProto));

                Assert.Multiple(() =>
                {
                    Assert.That(entMan.Count<GhostRoleComponent>(), Is.GreaterThanOrEqualTo(total));
                    Assert.That(entMan.Count<SpecForceComponent>(), Is.GreaterThanOrEqualTo(total));
                });

                var ghostRoles = entMan.EntityQueryEnumerator<GhostRoleComponent>();
                while (ghostRoles.MoveNext(out var uid, out var ghostRole))
                {
                    Assert.That(uid.Valid);

                    Assert.Multiple(() =>
                    {
                        Assert.That(ghostRole.RoleName, Is.Not.EqualTo("Unknown"));
                        Assert.That(ghostRole.RoleDescription, Is.Not.EqualTo("Unknown"));
                    });
                }
            }
        });

        await pair.CleanReturnAsync();
    }
}
