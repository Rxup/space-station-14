using Content.IntegrationTests.Fixtures;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reaction;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Chemistry;

[TestFixture]
public sealed class CreateDoughInCanTest : GameTest
{
    private static readonly ProtoId<ReactionPrototype> CreateDoughReaction = "CreateDough";

    [Test]
    public async Task CreateDoughInSodaCanSpawnsVisibleDough()
    {
        var pair = Pair;
        var server = pair.Server;
        var entityManager = server.ResolveDependency<IEntityManager>();
        var prototypeManager = server.ResolveDependency<IPrototypeManager>();
        var solutionContainerSystem = entityManager.System<SharedSolutionContainerSystem>();
        var containerSystem = entityManager.System<SharedContainerSystem>();
        var testMap = await pair.CreateTestMap();

        var reaction = prototypeManager.Index(CreateDoughReaction);

        EntityUid can = default;
        Entity<SolutionComponent>? drinkSolution = default;

        await server.WaitAssertion(() =>
        {
            can = entityManager.SpawnEntity("DrinkColaCanEmpty", testMap.GridCoords);
            Assert.That(
                solutionContainerSystem.TryGetSolution(can, "drink", out drinkSolution, out _),
                Is.True);

            foreach (var (id, reactant) in reaction.Reactants)
            {
#pragma warning disable NUnit2045
                Assert.That(
                    solutionContainerSystem.TryAddReagent(
                        drinkSolution!.Value,
                        id,
                        reactant.Amount,
                        out var quantity,
                        reaction.MinimumTemperature),
                    Is.True);
                Assert.That(quantity, Is.EqualTo(reactant.Amount));
#pragma warning restore NUnit2045
            }
        });

        await server.WaitIdleAsync();

        await server.WaitAssertion(() =>
        {
            var doughCount = 0;
            EntityUid? dough = null;

            var query = entityManager.AllEntityQueryEnumerator<MetaDataComponent>();
            while (query.MoveNext(out var uid, out var meta))
            {
                if (meta.EntityPrototype?.ID != "FoodDough")
                    continue;

                doughCount++;
                dough = uid;
            }

            Assert.That(doughCount, Is.EqualTo(1));
            Assert.That(dough, Is.Not.Null);
            Assert.That(containerSystem.IsEntityInContainer(dough!.Value), Is.False);
        });
    }
}
