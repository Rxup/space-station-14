using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Shared.Backmen.Surgery;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Store;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Backmen.Body;

[TestFixture]
public sealed class SpeciesBUiTest : GameTest
{
    private const string BaseMobSpeciesTestId = "BaseMobSpeciesTest";

    private static PoolSettings PsDirtyDisconnected => new()
    {
        Dirty = true,
        Connected = false,
    };
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  name: BaseMobSpeciesTest
  id: BaseMobSpeciesTest
  parent: BaseSpeciesMob
";

    private Dictionary<Enum, InterfaceData> GetInterfaces(UserInterfaceComponent comp) =>
        (Dictionary<Enum, InterfaceData>)
            typeof(UserInterfaceComponent).GetField("Interfaces", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(comp);

    [Test]
    [PairConfig(nameof(PsDirtyDisconnected))]
    public async Task AllSpeciesHaveBaseBUiTest()
    {
        var proto = Server.ResolveDependency<IPrototypeManager>();
        var factoryComp = Server.ResolveDependency<IComponentFactory>();

        await Server.WaitAssertion(() =>
        {
            var bUiSys = Server.System<SharedUserInterfaceSystem>();

            Assert.That(proto.TryIndex(BaseMobSpeciesTestId, out var baseEnt), Is.True);
            Assert.That(baseEnt, Is.Not.Null);
            Assert.That(baseEnt.TryGetComponent<UserInterfaceComponent>(out var bUiBase, factoryComp), Is.True);
            Assert.That(bUiBase, Is.Not.Null);
            var baseKeys = GetInterfaces(bUiBase).Keys.ToArray();

            foreach (var specie in proto.EnumeratePrototypes<SpeciesPrototype>())
            {
                var ent = proto.Index(specie.Prototype);
                Assert.That(ent.TryGetComponent<UserInterfaceComponent>(out var bUi, factoryComp), Is.True);
                Assert.That(bUi, Is.Not.Null);
                var states = GetInterfaces(bUiBase);
                foreach (var key in baseKeys)
                {
                    Assert.That(states.ContainsKey(key), Is.True, $"Species {specie.ID} has not UserInterface of type enum.{key.GetType().Name}");
                }
            }
        });
    }
}
