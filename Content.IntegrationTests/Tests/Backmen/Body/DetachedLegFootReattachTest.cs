using Content.IntegrationTests.Fixtures;
using Content.Shared.Backmen.Body.OrganRelations;
using Content.Shared.Backmen.Body.Systems;
using Content.Shared.Body;
using Content.Shared.Body.Organ;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Backmen.Body;

/// <summary>
/// Reattaching a leg from a detached limb bundle must not delete the paired foot.
/// </summary>
[TestFixture]
public sealed class DetachedLegFootReattachTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [Test]
    public async Task ReattachingLegFromDetachedBundle_PreservesFoot()
    {
        var map = await Pair.CreateTestMap();
        NetEntity netPatient = default;
        NetEntity netFoot = default;

        await Server.WaitAssertion(() =>
        {
            var bodySys = Server.EntMan.System<BkmBodySharedSystem>();
            var organRelations = Server.EntMan.System<OrganRelationInitializerSystem>();

            var patient = Server.EntMan.SpawnEntity("MobHuman", map.MapCoords);
            var bundle = Server.EntMan.SpawnEntity("BackmenDetachedBody", map.MapCoords);
            var leg = Server.EntMan.SpawnEntity("OrganHumanLegLeft", MapCoordinates.Nullspace);
            var foot = Server.EntMan.SpawnEntity("OrganHumanFootLeft", MapCoordinates.Nullspace);

            Assert.That(bodySys.InsertOrganIntoBody(bundle, leg), Is.True);
            Assert.That(bodySys.InsertOrganIntoBody(bundle, foot), Is.True);

            organRelations.WireRelationships((bundle, Server.EntMan.GetComponent<BodyComponent>(bundle)));

            Assert.That(bodySys.InsertOrganIntoBody(patient, leg), Is.True,
                "Leg should transfer from detached bundle to patient.");

            Assert.That(Server.EntMan.EntityExists(foot) && !Server.EntMan.IsQueuedForDeletion(foot), Is.True,
                "Foot must survive leg removal from detached bundle.");

            Assert.That(bodySys.InsertOrganIntoBody(patient, foot), Is.True,
                "Foot should transfer from detached bundle to patient.");

            netPatient = Server.EntMan.GetNetEntity(patient);
            netFoot = Server.EntMan.GetNetEntity(foot);
        });

        await Server.WaitIdleAsync();

        await Server.WaitAssertion(() =>
        {
            var patient = Server.EntMan.GetEntity(netPatient);
            var foot = Server.EntMan.GetEntity(netFoot);
            var bodySys = Server.EntMan.System<BodySystem>();

            Assert.That(Server.EntMan.EntityExists(foot), Is.True);
            Assert.That(bodySys.TryGetOrganByCategory(patient, "FootLeft", out _), Is.True);
        });
    }
}
