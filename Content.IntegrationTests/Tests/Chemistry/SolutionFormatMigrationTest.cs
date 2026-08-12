#nullable enable
using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Components.SolutionManager;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Sequence;
using Robust.Shared.Serialization.Markdown.Value;

namespace Content.IntegrationTests.Tests.Chemistry;

/// <summary>
/// Guards the chemistry solution migration off obsolete <see cref="SolutionContainerManagerComponent"/>.
/// New format: the container entity itself has <see cref="SolutionComponent"/> (via SolutionDrink / SolutionTiny / etc).
/// Hybrid (both Solution + SCM on one prototype) asserts on client entity-state apply.
/// </summary>
[TestFixture]
public sealed class SolutionFormatMigrationTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    /// <summary>
    /// Prototypes that must already use the new format (Solution on entity, no SCM).
    /// Includes abstract bases (checked via composed mapping — abstract entities are not in ProtoMan index).
    /// Expand this list as more content is migrated.
    /// </summary>
    private static readonly string[] RequiredNewFormat =
    [
        "DrinkBase",
        "DrinkBaseEmptyTrash",
        "CrazyGlue",
        "CrazyLube",
        "ChemistryEmptyVial",
        "ChemistryEmptyVialSmall",
        "VestineChemistryVial",
        "PlasmaChemistryVial",
        "RadiumChemistryVial",
        "ChlorineChemistryVial",
        "BaseChemistryEmptyBottle",
        "DrinkCanBase",
    ];

    [Test]
    public async Task NoPrototypeMayCombineSolutionAndSolutionContainerManager()
    {
        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                foreach (var proto in Server.ProtoMan.EnumeratePrototypes<EntityPrototype>())
                {
                    var hasSolution = proto.Components.ContainsKey("Solution");
                    var hasScm = proto.Components.ContainsKey("SolutionContainerManager");
                    if (!hasSolution || !hasScm)
                        continue;

                    Assert.Fail(
                        $"{proto.ID}: hybrid Solution + SolutionContainerManager. " +
                        "Migrate SCM fills to `- type: Solution` (entity-solution format).");
                }
            });
        });
    }

    [Test]
    public async Task RequiredPrototypesUseNewSolutionFormat()
    {
        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                foreach (var id in RequiredNewFormat)
                {
                    if (Server.ProtoMan.TryIndex<EntityPrototype>(id, out var proto))
                    {
                        Assert.That(proto.Components.ContainsKey("Solution"), Is.True,
                            $"{id}: expected Solution component (new format)");
                        Assert.That(proto.Components.ContainsKey("SolutionContainerManager"), Is.False,
                            $"{id}: still has obsolete SolutionContainerManager");
                        continue;
                    }

                    // Abstract entity prototypes are not instantiated into the index.
                    Assert.That(Server.ProtoMan.TryGetMapping<EntityPrototype>(id, out var mapping), Is.True,
                        $"Missing prototype {id}");
                    if (mapping == null)
                        continue;

                    Assert.That(MappingHasComponent(mapping, "Solution"), Is.True,
                        $"{id}: expected Solution component (new format)");
                    Assert.That(MappingHasComponent(mapping, "SolutionContainerManager"), Is.False,
                        $"{id}: still has obsolete SolutionContainerManager");
                }
            });
        });
    }

    private static bool MappingHasComponent(MappingDataNode mapping, string componentName)
    {
        if (!mapping.TryGet("components", out SequenceDataNode? components))
            return false;

        foreach (var node in components)
        {
            if (node is not MappingDataNode comp)
                continue;
            if (!comp.TryGet("type", out ValueDataNode? typeNode))
                continue;
            if (typeNode.Value == componentName)
                return true;
        }

        return false;
    }

    [Test]
    public async Task RequiredConcreteProtosSpawnWithoutSolutionAssertOnClient()
    {
        var map = await Pair.CreateTestMap();
        var concrete = new List<string>();

        await Server.WaitAssertion(() =>
        {
            foreach (var id in RequiredNewFormat)
            {
                if (!Server.ProtoMan.TryIndex<EntityPrototype>(id, out var proto) || proto.Abstract)
                    continue;
                concrete.Add(id);
            }
        });

        var nets = new List<(string Id, NetEntity Net)>();
        await Server.WaitAssertion(() =>
        {
            foreach (var id in concrete)
            {
                var ent = Server.EntMan.SpawnEntity(id, map.MapCoords);
                Assert.That(Server.EntMan.HasComponent<SolutionComponent>(ent), Is.True,
                    $"{id}: spawned entity should be a Solution itself");
                Assert.That(Server.EntMan.HasComponent<SolutionContainerManagerComponent>(ent), Is.False,
                    $"{id}: SCM should be gone after mapinit (or never present)");
                nets.Add((id, Server.EntMan.GetNetEntity(ent)));
            }
        });

        await Pair.RunTicksSync(10);

        await Client.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                foreach (var (id, net) in nets)
                {
                    var ent = Client.EntMan.GetEntity(net);
                    Assert.That(Client.EntMan.EntityExists(ent), Is.True, $"{id}: missing on client");
                    Assert.That(Client.EntMan.HasComponent<SolutionComponent>(ent), Is.True,
                        $"{id}: client entity should have Solution");
                }
            });
        });
    }
}
