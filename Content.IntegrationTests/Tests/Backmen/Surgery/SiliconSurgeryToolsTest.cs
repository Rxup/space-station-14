using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server.Backmen.Surgery;
using Content.Server.Tools.Innate;
using Content.Shared.Backmen.Body.Systems;
using Content.Shared.Backmen.CCVar;
using Content.Shared.Body.Part;
using Content.Shared.Hands.EntitySystems;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Backmen.Surgery;

[TestFixture]
[EnsureCVar(Side.Server, typeof(CCVars), nameof(CCVars.PainEnabled), true)]
public sealed class SiliconSurgeryToolsTest : GameTest
{
    private static readonly EntProtoId DroneMed = "DroneBPLAMED";
    private static readonly EntProtoId ScalpelStep = "SurgeryStepCarefulIncisionScalpel";

    public override PoolSettings PoolSettings => new()
    {
        Connected = true,
        Dirty = true,
    };

    [Test]
    public async Task MedDrone_SpawnsThreeInnateTools()
    {
        var map = await Pair.CreateTestMap();
        var handsSys = Server.EntMan.System<SharedHandsSystem>();
        EntityUid drone = default;

        await Server.WaitPost(() => drone = Server.EntMan.SpawnAtPosition(DroneMed, map.GridCoords));
        await Server.WaitIdleAsync();
        await Pair.RunTicksSync(2);

        await Server.WaitAssertion(() =>
        {
            Assert.That(Server.EntMan.TryGetComponent(drone, out InnateToolComponent? innate), Is.True);
            Assert.That(innate!.ToolUids.Count, Is.EqualTo(3));
            Assert.That(handsSys.EnumerateHands(drone).Count(), Is.GreaterThanOrEqualTo(3));
        });
    }

    [Test]
    public async Task MedDrone_CanPerformScalpelStepWithOmnimedTool()
    {
        var map = await Pair.CreateTestMap();
        var surgerySys = Server.EntMan.System<SurgerySystem>();
        EntityUid drone = default;
        EntityUid patient = default;
        EntityUid step = default;

        await Server.WaitPost(() =>
        {
            drone = Server.EntMan.SpawnAtPosition(DroneMed, map.GridCoords);
            patient = Server.EntMan.SpawnAtPosition("MobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
            step = Server.EntMan.Spawn(ScalpelStep);
        });

        await Server.WaitIdleAsync();
        await Pair.RunTicksSync(2);

        await Server.WaitAssertion(() =>
        {
            var bodySys = Server.EntMan.System<BkmBodySharedSystem>();
            Assert.That(bodySys.TryGetWoundableTargetByType(patient, BodyPartType.Chest, null, out var chest), Is.True);

            Assert.That(
                surgerySys.CanPerformStep(drone, patient, chest, step, false),
                Is.True,
                "Med drone should perform scalpel step with OmnimedTool in innate hands.");
        });
    }
}
