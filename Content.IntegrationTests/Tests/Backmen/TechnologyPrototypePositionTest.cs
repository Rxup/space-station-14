using System.Collections.Generic;
using System.Numerics;
using Content.Shared.Research.Prototypes;
using Robust.Shared.Prototypes;
namespace Content.IntegrationTests.Tests.Backmen;

[TestFixture]
public sealed class TechnologyPrototypePositionTests
{
    [Test]
    public async Task TechnologyPrototypePositionsAreUniqueTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var protoManager = server.ResolveDependency<IPrototypeManager>();

        var fails = new List<string>();
        var positions = new Dictionary<Vector2, string>();

        await server.WaitAssertion(() =>
        {
            foreach (var techProto in protoManager.EnumeratePrototypes<TechnologyPrototype>())
            {
                Vector2 position = techProto.Position;

                if (!positions.TryAdd(position, techProto.Name))
                {
                    fails.Add($"ID: {techProto.ID} Position - {position}. Conflicts with ID: {positions[position]}");
                }
            }
        });

        if (fails.Count > 0)
        {
            var msg = string.Join("\n", fails) + "\n" + "Проверь позиции технологий, данная позиция занята другой технологией!";
            Assert.Fail(msg);
        }

        await pair.CleanReturnAsync();
    }
}
