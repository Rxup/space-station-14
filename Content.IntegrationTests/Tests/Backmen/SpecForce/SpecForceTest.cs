#nullable enable
using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server.Backmen.SpecForces;
using Content.Server.Ghost.Roles.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Backmen.Specforce;

[TestFixture]
public sealed class SpecForceTest : GameTest
{
    private static PoolSettings PsDirtyNoDummyTickerNoLobby => new()
    {
        Dirty = true,
        Connected = true,
        DummyTicker = false,
        InLobby = false,
    };

    /// <summary>
    /// A list of spec forces that can be ignored by this test.
    /// </summary>
    private readonly HashSet<string> _ignoredPrototypes = new() {};

    [Test]
    [PairConfig(nameof(PsDirtyNoDummyTickerNoLobby))]
    public async Task CallSpecForces()
    {
        var protoManager = Server.ResolveDependency<IPrototypeManager>();
        var entSysManager = Server.ResolveDependency<IEntitySystemManager>();
        var entMan = Server.EntMan;
        var specForceSystem = entSysManager.GetEntitySystem<SpecForcesSystem>();

        // Try to spawn every SpecForceTeam
        await Server.WaitAssertion(() =>
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
    }
}
