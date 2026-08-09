using Content.IntegrationTests.Fixtures;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Backmen.Flesh;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Store.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Physics;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Backmen.Flesh;

/// <summary>
/// Flesh cultist corpse devour: TargetAction wiring and do-after completion without crash.
/// </summary>
[TestFixture]
public sealed class FleshCultistDevourTest : GameTest
{
    private static readonly EntProtoId FleshCultistDevour = "FleshCultistDevour";
    private static readonly EntProtoId MobHuman = "MobHuman";

    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  parent: MobHuman
  id: MobFleshCultistDevourTest
  components:
  - type: FleshCultist
    devourTime: 0
";

    public override PoolSettings PoolSettings => new()
    {
        Connected = true,
        Dirty = true,
    };

    [Test]
    public async Task FleshCultistDevour_HasTargetActionComponent()
    {
        await Server.WaitAssertion(() =>
        {
            var action = Server.EntMan.Spawn(FleshCultistDevour);
            Assert.That(Server.EntMan.HasComponent<TargetActionComponent>(action), Is.True,
                "FleshCultistDevour must include TargetAction (EntityTargetAction alone is not enough).");
            Assert.That(Server.EntMan.HasComponent<EntityTargetActionComponent>(action), Is.True);
            Server.EntMan.DeleteEntity(action);
        });
    }

    [Test]
    public async Task FleshCultistDevour_DevoursDeadHumanoidWithoutCrash()
    {
        var map = await Pair.CreateTestMap();
        var mobStateSys = Server.EntMan.System<MobStateSystem>();
        var actionsSys = Server.EntMan.System<SharedActionsSystem>();
        var doAfterSys = Server.EntMan.System<SharedDoAfterSystem>();

        EntityUid cultist = default;
        EntityUid corpse = default;
        FixedPoint2 hungerBefore = default;

        await Server.WaitPost(() =>
        {
            cultist = Server.EntMan.Spawn("MobFleshCultistDevourTest", map.MapCoords);
            corpse = Server.EntMan.Spawn(MobHuman, map.MapCoords);

            Assert.That(Server.EntMan.HasComponent<FleshCultistComponent>(cultist), Is.True);
            Assert.That(Server.EntMan.HasComponent<StoreComponent>(cultist), Is.True);

            var cultistComp = Server.EntMan.GetComponent<FleshCultistComponent>(cultist);
            Assert.That(cultistComp.FleshCultistDevour, Is.Not.Null);
            var devourAction = cultistComp.FleshCultistDevour!.Value;

            Assert.That(Server.EntMan.HasComponent<TargetActionComponent>(devourAction), Is.True);
            Assert.That(
                actionsSys.ValidateEntityTarget(
                    cultist,
                    corpse,
                    (devourAction, Server.EntMan.GetComponent<EntityTargetActionComponent>(devourAction))),
                Is.True,
                "ValidateEntityTarget requires TargetAction and must not throw.");

            hungerBefore = cultistComp.Hunger;
            mobStateSys.ChangeMobState(corpse, MobState.Dead);

            var started = doAfterSys.TryStartDoAfter(new DoAfterArgs(
                Server.EntMan,
                cultist,
                TimeSpan.Zero,
                new FleshCultistDevourDoAfterEvent(),
                cultist,
                target: corpse,
                used: cultist)
            {
                BreakOnMove = false,
                NeedHand = false,
                RequireCanInteract = false,
            });
            Assert.That(started, Is.True);
        });

        await Pair.RunTicksSync(5);

        await Server.WaitAssertion(() =>
        {
            Assert.That(Server.EntMan.EntityExists(cultist), Is.True);
            Assert.That(Server.EntMan.EntityExists(corpse), Is.True,
                "Humanoid corpses should remain as stripped skeletons after devour.");

            var cultistComp = Server.EntMan.GetComponent<FleshCultistComponent>(cultist);
            Assert.That(cultistComp.Hunger, Is.GreaterThan(hungerBefore),
                "Devour should increase cultist hunger.");

            Assert.That(Server.EntMan.TryGetComponent(corpse, out FixturesComponent? fixtures), Is.True);
            Assert.That(fixtures!.Fixtures.TryGetValue("fix1", out var fixture), Is.True);
            Assert.That(fixture!.Density, Is.EqualTo(10f).Within(0.01f),
                "Devoured body density should be reduced.");
        });
    }
}