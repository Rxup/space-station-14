using Content.IntegrationTests.Fixtures;
using Content.Server.Backmen.Body.Systems;
using Content.Shared.Backmen.Body.OrganRelations;
using Content.Shared.Backmen.Body.Systems;
using Content.Shared.Backmen.Surgery.Wounds.Components;
using Content.Shared.Backmen.Surgery.Wounds.Systems;
using Content.Shared.Body;
using Content.Shared.Body.Part;
using Content.Shared.Body.Organ;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using System.Collections.Generic;
using System.Numerics;

namespace Content.IntegrationTests.Tests.Backmen.Body;

/// <summary>
/// Full-body gib must scatter external parts into separate <see cref="BkmDetachedBodyComponent"/> bundles.
/// </summary>
[TestFixture]
public sealed class GibDetachedBodyTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [Test]
    public async Task GibBody_CreatesMultipleDetachedBundles()
    {
        var map = await Pair.CreateTestMap();
        NetEntity netPatient = default;

        await Server.WaitAssertion(() =>
        {
            var patient = Server.EntMan.SpawnEntity("MobHuman", map.MapCoords);
            netPatient = Server.EntMan.GetNetEntity(patient);
            var bodySys = Server.EntMan.System<BkmBodySystem>();
            bodySys.GibBody(patient, gibOrgans: true);
        });

        await Server.WaitRunTicks(90);

        await Server.WaitAssertion(() =>
        {
            Assert.That(Server.EntMan.EntityExists(Server.EntMan.GetEntity(netPatient)), Is.False,
                "Gib must delete the mob shell after scattering organs.");

            var bundleCount = 0;
            var positions = new List<Vector2>();
            var enumerator = Server.EntMan.EntityQueryEnumerator<BkmDetachedBodyComponent>();
            while (enumerator.MoveNext(out var bundle, out var detached))
            {
                bundleCount++;
                Assert.That(detached.MessyScatter, Is.True, "Gib bundles should use violent scatter.");
                Assert.That(Server.EntMan.TryGetComponent(bundle, out BodyComponent? body) && body!.Organs?.Count > 0,
                    Is.True,
                    "Each detached bundle should still contain at least one organ.");
                positions.Add(Server.EntMan.GetComponent<TransformComponent>(bundle).WorldPosition);
            }

            Assert.That(bundleCount, Is.GreaterThan(3),
                "Human gib should produce a detached bundle per external part, not a single pile.");

            Assert.That(MedianPairwiseDistance(positions), Is.GreaterThanOrEqualTo(1f),
                "Gib should scatter bundles at least one tile apart on median.");
        });
    }

    [Test]
    public async Task DestroyTorso_KeepsOrganInsideDetachedBundle()
    {
        var map = await Pair.CreateTestMap();
        NetEntity netPatient = default;

        await Server.WaitPost(() =>
        {
            var patient = Server.EntMan.SpawnEntity("MobHuman", map.MapCoords);
            netPatient = Server.EntMan.GetNetEntity(patient);
            var bodySys = Server.EntMan.System<BkmBodySystem>();
            var woundSys = Server.EntMan.System<WoundSystem>();

            Assert.That(bodySys.TryGetWoundableTargetByType(patient, BodyPartType.Chest, null, out var torso));
            Assert.That(bodySys.TryGetWoundableTargetByType(patient, BodyPartType.Arm, BodyPartSymmetry.Left, out _));
            var torsoWoundable = Server.EntMan.GetComponent<WoundableComponent>(torso);

            woundSys.DestroyWoundable(patient, torso, torsoWoundable);
        });

        await Server.WaitRunTicks(90);

        await Server.WaitAssertion(() =>
        {
            Assert.That(Server.EntMan.EntityExists(Server.EntMan.GetEntity(netPatient)), Is.False,
                "An organless mob shell must be removed after violent torso gib.");

            EntityUid torso = default;
            EntityUid? bundle = null;
            EntityUid? leftArm = default;
            var query = Server.EntMan.EntityQueryEnumerator<BkmDetachedBodyComponent, BodyComponent>();
            while (query.MoveNext(out var uid, out _, out var body))
            {
                foreach (var organ in body.Organs?.ContainedEntities ?? [])
                {
                    if (!Server.EntMan.TryGetComponent(organ, out OrganComponent? organComp) || organComp.Category is not { } category)
                        continue;

                    if (category == "Torso")
                    {
                        bundle = uid;
                        torso = organ;
                    }

                    if (category == "ArmLeft")
                        leftArm = organ;
                }
            }

            Assert.That(bundle, Is.Not.Null, "Torso destroy should create a detached bundle containing the torso organ.");
            Assert.That(Server.EntMan.EntityExists(torso), Is.True, "Torso organ must survive inside the detached bundle.");
            Assert.That(leftArm, Is.Not.Null);
            Assert.That(Server.EntMan.EntityExists(leftArm.Value), Is.True, "Left arm should detach into its own bundle.");

            var bundleCount = 0;
            EntityUid? armBundle = null;
            var allBundles = Server.EntMan.EntityQueryEnumerator<BkmDetachedBodyComponent, BodyComponent>();
            while (allBundles.MoveNext(out var uid, out _, out var bundleBody))
            {
                bundleCount++;

                if (bundleBody.Organs?.Contains(torso) == true)
                {
                    Assert.That(bundleBody.Organs?.Contains(leftArm.Value), Is.False,
                        "Torso gib must not sweep external limbs into the chest bundle.");
                    continue;
                }

                if (bundleBody.Organs?.Contains(leftArm.Value) == true)
                    armBundle = uid;
            }

            Assert.That(armBundle, Is.Not.Null, "Left arm should end up in a separate detached bundle.");

            Assert.That(bundleCount, Is.GreaterThan(1),
                "Destroying the torso should scatter external parts into separate bundles.");
        });
    }

    private static float MedianPairwiseDistance(List<Vector2> positions)
    {
        var distances = new List<float>();
        for (var i = 0; i < positions.Count; i++)
        {
            for (var j = i + 1; j < positions.Count; j++)
                distances.Add((positions[i] - positions[j]).Length());
        }

        if (distances.Count == 0)
            return 0f;

        distances.Sort();
        return distances[distances.Count / 2];
    }
}
