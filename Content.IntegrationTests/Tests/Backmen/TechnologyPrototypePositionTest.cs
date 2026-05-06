using System.Collections.Generic;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Research.Prototypes;
using Robust.Shared.Prototypes;
namespace Content.IntegrationTests.Tests.Backmen;

[TestFixture]
public sealed class TechnologyPrototypePositionTests : GameTest
{
    [Test]
    public async Task TechnologyPrototypePositionsAreUniqueTest()
    {
        var protoManager = Server.ResolveDependency<IPrototypeManager>();

        var fails = new List<string>();
        var positions = new Dictionary<Vector2, string>();

        await Server.WaitAssertion(() =>
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
    }
}
