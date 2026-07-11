using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Backmen.Targeting;

namespace Content.IntegrationTests.Tests.Backmen.Targeting;

[TestFixture]
public sealed class CombatTargetOddsPrototypeTest : GameTest
{
    [Test]
    public async Task ValidateSpreadWeights()
    {
        await Server.WaitAssertion(() =>
        {
            var protos = Server.ProtoMan.EnumeratePrototypes<CombatTargetOddsPrototype>()
                .Where(proto => !proto.Abstract)
                .ToList();

            Assert.That(protos, Is.Not.Empty);

            Assert.Multiple(() =>
            {
                foreach (var proto in protos)
                {
                    foreach (var (aimedPart, row) in proto.Spread)
                    {
                        var sum = row.Values.Sum();
                        Assert.That(sum, Is.LessThanOrEqualTo(1f + 1e-4f),
                            $"{proto.ID} spread[{aimedPart}] sum={sum} > 1.0");
                        Assert.That(row.Values, Is.All.GreaterThanOrEqualTo(0f),
                            $"{proto.ID} spread[{aimedPart}] has negative weight");
                        Assert.That(row.Count, Is.GreaterThan(0),
                            $"{proto.ID} spread[{aimedPart}] is empty");
                    }
                }
            });
        });
    }
}
