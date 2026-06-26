using Content.IntegrationTests.Tests.Interaction;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Content.Shared.Fluids.Components;
using Content.Shared.Nutrition.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using System.Numerics;

namespace Content.IntegrationTests.Tests.Chemistry;

public sealed class DrainTest : InteractionTest
{
    private static readonly EntProtoId PizzaPrototype = "FoodPizzaMargherita";
    private static readonly EntProtoId DrainPrototype = "FloorDrain";
    private static readonly EntProtoId BucketPrototype = "Bucket";
    private static readonly ProtoId<ReagentPrototype> WaterReagent = "Water";
    private static readonly FixedPoint2 WaterVolume = 50; // 50u
    private static readonly FixedPoint2 PuddleVolume = 30; // 30u
    private static readonly ProtoId<ReagentPrototype> PuddleReagent = "Blood";

    /// <summary>
    /// Tests that drag drop interactions with drains are working as intended.
    /// </summary>
    [Test]
    public async Task DragDropOntoDrainTest()
    {
        var solutionContainerSys = SEntMan.System<SharedSolutionContainerSystem>();

        // Spawn a drain one tile away.
        var drain = await Spawn(DrainPrototype);

        // Spawn a bucket at the player's coordinates.
        var bucket = await Spawn(BucketPrototype, PlayerCoords);

        // Add water to the bucket.
        Assert.That(solutionContainerSys.TryGetDrainableSolution(ToServer(bucket), out var solutionEnt, out var solution), "Bucket had no drainable solution.");
        await Server.WaitAssertion(() =>
        {
            Assert.That(solutionContainerSys.TryAddReagent(solutionEnt.Value, WaterReagent, WaterVolume), "Could not add water to the bucket.");
        });

        // Check that the bucket was filled.
        Assert.That(solutionContainerSys.TryGetDrainableSolution(ToServer(bucket), out solutionEnt, out solution), "Bucket had no drainable solution after filling it.");
        Assert.That(solution.Volume, Is.EqualTo(WaterVolume));

        // Drag drop the bucket onto the drain.
        await DragDrop(bucket, drain);

        // Check that the bucket is empty.
        Assert.That(solutionContainerSys.TryGetDrainableSolution(ToServer(bucket), out solutionEnt, out solution), "Bucket had no drainable solution after draining it.");
        Assert.That(solution.Volume, Is.EqualTo(FixedPoint2.Zero), "Bucket was not empty after draining it.");

        await Delete(bucket);

        // Spawn a pizza at the player's coordinates.
        var pizza = await Spawn(PizzaPrototype, PlayerCoords);

        // Check that the pizza is not empty.
        var edibleSolutionId = Comp<EdibleComponent>(pizza).Solution;
        Assert.That(solutionContainerSys.TryGetSolution(ToServer(pizza), edibleSolutionId, out solutionEnt, out solution), "Pizza had no edible solution.");
        var pizzaVolume = solution.Volume;
        Assert.That(pizzaVolume, Is.GreaterThan(FixedPoint2.Zero), "Pizza had no reagents inside its edible solution.");

        // Drag drop the pizza onto the drain.
        // Yes, this was a bug that existed before.
        await DragDrop(pizza, drain);

        // Check that the pizza did not get deleted or had its reagents drained.
        AssertExists(pizza);
        Assert.That(solutionContainerSys.TryGetSolution(ToServer(pizza), edibleSolutionId, out solutionEnt, out solution), "Pizza had no edible solution.");
        Assert.That(solution.Volume, Is.EqualTo(pizzaVolume), "Pizza lost reagents when drag dropped onto a drain.");
    }

    /// <summary>
    /// Tests that drains make puddles next to them disappear.
    /// </summary>
    [Test]
    public async Task DrainPuddleTest()
    {
        var solutionContainerSys = SEntMan.System<SharedSolutionContainerSystem>();

        // Spawn a puddle on the target tile (player occupies PlayerCoords).
        NetEntity puddle = default;
        await Server.WaitPost(() =>
        {
            var coords = Transform.WithEntityId(MapData.GridCoords.Offset(new Vector2(1.5f, 0.5f)), MapData.MapUid);
            var uid = SEntMan.SpawnAtPosition("Puddle", coords);
            Assert.That(solutionContainerSys.TryGetSolution(uid, "puddle", out var solEnt, out var sol), Is.True);
            sol.AddReagent(PuddleReagent, PuddleVolume);
            solutionContainerSys.UpdateChemicals(solEnt!.Value);
            puddle = SEntMan.GetNetEntity(uid);
        });
        await RunTicks(5);
        Assert.That(puddle, Is.Not.EqualTo(NetEntity.Invalid));

        // If you are a fork that made more reagents evaporate, change PuddleReagent above.
        Assert.That(HasComp<EvaporationComponent>(puddle), Is.False, "The chosen reagent is evaporating on its own and we cannot use it for the drain test.");

        var puddleSolutionId = Comp<PuddleComponent>(puddle).SolutionName;
        Assert.That(solutionContainerSys.TryGetSolution(ToServer(puddle), puddleSolutionId, out _, out var solution), "Puddle had no solution.");
        Assert.That(solution.Volume, Is.EqualTo(PuddleVolume), "Puddle had the wrong amount of reagents after spawning.");

        // Wait a few seconds and check that the puddle did not disappear on its own.
        await RunSeconds(10);
        Assert.That(solutionContainerSys.TryGetSolution(ToServer(puddle), puddleSolutionId, out _, out solution), "Puddle had no solution.");
        Assert.That(solution.Volume, Is.EqualTo(PuddleVolume), "Puddle had the wrong amount of reagents after spawning.");

        // Move the player off the drain tile before spawning it.
        await Server.WaitPost(() => Transform.SetCoordinates(SPlayer, SEntMan.GetCoordinates(TargetCoords)));
        await RunTicks(5);

        // Spawn a drain one tile away from the puddle.
        await Spawn(DrainPrototype, PlayerCoords);

        // Wait a few seconds.
        await RunSeconds(10);

        // Make sure the puddle was deleted by the drain.
        AssertDeleted(puddle);
    }
}
