using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server.Backmen.Body.Systems;
using Content.Shared.Administration.Systems;
using Content.Shared.Backmen.Body.Systems;
using Content.Shared.Backmen.Targeting;
using Content.Shared.Backmen.Surgery.Consciousness.Components;
using Content.Shared.Backmen.Surgery.Consciousness.Systems;
using Content.Shared.Backmen.Surgery.Wounds.Components;
using Content.Shared.Backmen.Surgery.Wounds.Systems;
using Content.Shared.Body;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Part;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Humanoid.Prototypes;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Backmen.Body;

[TestFixture]
public sealed class GibOnDamageTest : GameTest
{
    /// <summary>
    /// Species that can be ignored by this test (same as <see cref="BodySetupTest"/>).
    /// </summary>
    private readonly HashSet<string> _ignoredPrototypes = new()
    {
        "Skeleton",
        "Monkey",
    };

    public override PoolSettings PoolSettings => new()
    {
        Dirty = true,
        Connected = false,
        InLobby = false,
    };

    [Test]
    public async Task MassiveDamageDestroysWoundablesWithoutEnumerationCrash()
    {
        var entMan = Server.ResolveDependency<IEntityManager>();
        var bodySystem = entMan.System<BkmBodySystem>();
        var nubodySystem = entMan.System<BodySystem>();
        var woundSystem = entMan.System<WoundSystem>();
        var rejuvenateSystem = entMan.System<RejuvenateSystem>();
        var consciousnessSystem = entMan.System<ConsciousnessSystem>();

        var testMap = await Pair.CreateTestMap();

        var torsoCategory = new ProtoId<OrganCategoryPrototype>("Torso");
        var headCategory = new ProtoId<OrganCategoryPrototype>("Head");

        await Server.WaitAssertion(() =>
        {
            foreach (var speciesPrototype in Server.ProtoMan.EnumeratePrototypes<SpeciesPrototype>())
            {
                if (_ignoredPrototypes.Contains(speciesPrototype.ID))
                    continue;

                var speciesId = speciesPrototype.ID;
                var mob = entMan.Spawn(speciesPrototype.Prototype, testMap.MapCoords);

                try
                {
                    var initialWoundables = bodySystem.GetWoundableTargets(mob).Count();
                    Assert.That(
                        initialWoundables,
                        Is.GreaterThan(0),
                        $"Species should have external woundables: {speciesId}");

                    var initialArmCount = bodySystem.GetBodyPartCount(mob, BodyPartType.Arm);
                    var initialLegCount = bodySystem.GetBodyPartCount(mob, BodyPartType.Leg);

                    Assert.That(
                        bodySystem.TryGetWoundableTargetByType(mob, BodyPartType.Chest, null, out var torsoForDamage),
                        $"Torso should exist before damage: {speciesId}");
                    Assert.That(
                        entMan.TryGetComponent(torsoForDamage, out WoundableComponent? torsoWoundable),
                        $"Torso woundable should exist before damage: {speciesId}");

                    // spawn → damage → checks
                    for (var i = 0; i < 5 && !entMan.Deleted(mob); i++)
                        woundSystem.TryInduceWound(torsoForDamage, "Blunt", 25f, out _, torsoWoundable);

                    AssertDamagedButIntact(
                        entMan,
                        bodySystem,
                        woundSystem,
                        mob,
                        torsoForDamage,
                        speciesId,
                        initialWoundables,
                        initialArmCount,
                        initialLegCount);

                    // rejuvenate → checks
                    rejuvenateSystem.PerformRejuvenate(mob);
                    AssertFullExternalBody(
                        entMan,
                        bodySystem,
                        consciousnessSystem,
                        mob,
                        speciesId,
                        initialWoundables,
                        initialArmCount,
                        initialLegCount);

                    // gib legs and arms → checks
                    GibLimbs(entMan, bodySystem, woundSystem, mob);
                    AssertLimbsDestroyed(
                        bodySystem,
                        mob,
                        speciesId,
                        initialArmCount,
                        initialLegCount);

                    // rejuvenate → checks
                    rejuvenateSystem.PerformRejuvenate(mob);
                    AssertFullExternalBody(
                        entMan,
                        bodySystem,
                        consciousnessSystem,
                        mob,
                        speciesId,
                        initialWoundables,
                        initialArmCount,
                        initialLegCount);

                    var hasBrain = nubodySystem.TryGetOrganByCategory((mob, null), "Brain", out var brain);

                    // selective gib → rejuvenate
                    SelectiveGibExceptTorsoAndHead(
                        entMan,
                        bodySystem,
                        woundSystem,
                        mob,
                        torsoCategory,
                        headCategory);
                    AssertOnlyTorsoAndHeadRemain(
                        bodySystem,
                        nubodySystem,
                        mob,
                        speciesId,
                        hasBrain,
                        ref brain);

                    rejuvenateSystem.PerformRejuvenate(mob);
                    AssertFullExternalBody(
                        entMan,
                        bodySystem,
                        consciousnessSystem,
                        mob,
                        speciesId,
                        initialWoundables,
                        initialArmCount,
                        initialLegCount);

                    if (hasBrain)
                    {
                        Assert.That(
                            nubodySystem.TryGetOrganByCategory((mob, null), "Brain", out brain),
                            $"Brain should be restored before head gib: {speciesId}");
                        Assert.That(
                            brain.Comp.Body,
                            Is.EqualTo(mob),
                            $"Brain should be inside the body before head gib: {speciesId}");
                    }

                    // head gib → checks
                    Assert.That(
                        bodySystem.TryGetWoundableTargetByType(mob, BodyPartType.Head, null, out var headEntity),
                        $"Head should exist before head gib: {speciesId}");
                    Assert.That(
                        entMan.TryGetComponent(headEntity, out WoundableComponent? headWoundable),
                        $"Head woundable should exist before head gib: {speciesId}");

                    woundSystem.DestroyWoundable(mob, headEntity, headWoundable);

                    if (hasBrain)
                    {
                        Assert.That(
                            nubodySystem.TryGetOrganByCategory((mob, null), "Brain", out _),
                            Is.False,
                            $"Head gib should detach the brain from the body: {speciesId}");
                        Assert.That(
                            entMan.EntityExists(brain),
                            $"Brain entity should exist after head gib: {speciesId}");
                        Assert.That(
                            entMan.HasComponent<BrainComponent>(brain),
                            $"Brain component should exist after head gib: {speciesId}");
                    }

                    var remainingExternal = bodySystem.GetWoundableTargets(mob).ToList();
                    Assert.That(
                        remainingExternal.Count,
                        Is.EqualTo(initialWoundables - 1),
                        $"Head gib should remove head but leave other external woundables: {speciesId}");
                    Assert.That(
                        bodySystem.TryGetWoundableTargetByType(mob, BodyPartType.Head, null, out _),
                        Is.False,
                        $"Head should be gone after head gib: {speciesId}");
                    Assert.That(
                        bodySystem.TryGetWoundableTargetByType(mob, BodyPartType.Chest, null, out _),
                        Is.True,
                        $"Torso should remain after head gib: {speciesId}");
                }
                finally
                {
                    if (entMan.EntityExists(mob))
                        entMan.DeleteEntity(mob);
                }
            }
        });
    }

    private static void AssertDamagedButIntact(
        IEntityManager entMan,
        BkmBodySystem bodySystem,
        WoundSystem woundSystem,
        EntityUid mob,
        EntityUid torsoForDamage,
        string speciesId,
        int initialWoundables,
        int initialArmCount,
        int initialLegCount)
    {
        Assert.That(entMan.EntityExists(mob), $"Mob should survive applied damage: {speciesId}");
        Assert.That(
            bodySystem.GetWoundableTargets(mob).Count(),
            Is.EqualTo(initialWoundables),
            $"Damage should not destroy external woundables: {speciesId}");
        if (initialArmCount > 0)
            Assert.That(
                bodySystem.GetBodyPartCount(mob, BodyPartType.Arm),
                Is.EqualTo(initialArmCount),
                $"Damage should not destroy arms: {speciesId}");
        if (initialLegCount > 0)
            Assert.That(
                bodySystem.GetBodyPartCount(mob, BodyPartType.Leg),
                Is.EqualTo(initialLegCount),
                $"Damage should not destroy legs: {speciesId}");
        Assert.That(
            bodySystem.TryGetWoundableTargetByType(mob, BodyPartType.Head, null, out _),
            $"Head should remain after damage: {speciesId}");
        Assert.That(
            bodySystem.TryGetWoundableTargetByType(mob, BodyPartType.Chest, null, out _),
            $"Torso should remain after damage: {speciesId}");

        Assert.That(
            woundSystem.GetWoundableSeverityPoint(torsoForDamage),
            Is.GreaterThan(FixedPoint2.Zero),
            $"Mob should have damage after blunt hits: {speciesId}");
    }

    private static void AssertFullExternalBody(
        IEntityManager entMan,
        BkmBodySystem bodySystem,
        ConsciousnessSystem consciousnessSystem,
        EntityUid mob,
        string speciesId,
        int initialWoundables,
        int initialArmCount,
        int initialLegCount)
    {
        Assert.That(
            bodySystem.GetWoundableTargets(mob).Count(),
            Is.EqualTo(initialWoundables),
            $"Rejuvenate should restore all external woundables: {speciesId}");
        if (initialArmCount > 0)
            Assert.That(
                bodySystem.GetBodyPartCount(mob, BodyPartType.Arm),
                Is.EqualTo(initialArmCount),
                $"Rejuvenate should restore arms: {speciesId}");
        if (initialLegCount > 0)
            Assert.That(
                bodySystem.GetBodyPartCount(mob, BodyPartType.Leg),
                Is.EqualTo(initialLegCount),
                $"Rejuvenate should restore legs: {speciesId}");
        Assert.That(
            bodySystem.TryGetWoundableTargetByType(mob, BodyPartType.Head, null, out _),
            $"Rejuvenate should restore head: {speciesId}");
        Assert.That(
            bodySystem.TryGetWoundableTargetByType(mob, BodyPartType.Chest, null, out _),
            $"Rejuvenate should restore torso: {speciesId}");

        if (entMan.TryGetComponent(mob, out ConsciousnessComponent? consciousness))
            Assert.That(
                consciousnessSystem.CheckConscious((mob, consciousness)),
                $"Rejuvenate should restore consciousness: {speciesId}");

        if (entMan.TryGetComponent(mob, out DamageableComponent? damageable))
            Assert.That(
                damageable.TotalDamage,
                Is.EqualTo(FixedPoint2.Zero),
                $"Rejuvenate should clear damage: {speciesId}");
    }

    private static void AssertLimbsDestroyed(
        BkmBodySystem bodySystem,
        EntityUid mob,
        string speciesId,
        int initialArmCount,
        int initialLegCount)
    {
        if (initialArmCount > 0)
            Assert.That(
                bodySystem.GetBodyPartCount(mob, BodyPartType.Arm),
                Is.EqualTo(0),
                $"Arms should be destroyed after limb gib: {speciesId}");
        if (initialLegCount > 0)
            Assert.That(
                bodySystem.GetBodyPartCount(mob, BodyPartType.Leg),
                Is.EqualTo(0),
                $"Legs should be destroyed after limb gib: {speciesId}");
    }

    private static void AssertOnlyTorsoAndHeadRemain(
        BkmBodySystem bodySystem,
        BodySystem nubodySystem,
        EntityUid mob,
        string speciesId,
        bool hasBrain,
        ref Entity<OrganComponent> brain)
    {
        var remainingExternal = bodySystem.GetWoundableTargets(mob).ToList();
        Assert.That(
            remainingExternal.Count,
            Is.EqualTo(2),
            $"Only head and torso should remain after selective gib: {speciesId}");
        Assert.That(
            bodySystem.TryGetWoundableTargetByType(mob, BodyPartType.Head, null, out _),
            $"Head should remain after selective gib: {speciesId}");
        Assert.That(
            bodySystem.TryGetWoundableTargetByType(mob, BodyPartType.Chest, null, out _),
            $"Torso should remain after selective gib: {speciesId}");

        if (hasBrain)
        {
            Assert.That(
                nubodySystem.TryGetOrganByCategory((mob, null), "Brain", out brain),
                $"Brain should remain inside the body after selective gib: {speciesId}");
            Assert.That(
                brain.Comp.Body,
                Is.EqualTo(mob),
                $"Brain should remain inside the body after selective gib: {speciesId}");
        }
    }

    private static void GibLimbs(
        IEntityManager entMan,
        BkmBodySystem bodySystem,
        WoundSystem woundSystem,
        EntityUid mob)
    {
        DestroyWoundableIfPresent(entMan, bodySystem, woundSystem, mob, BodyPartType.Arm, BodyPartSymmetry.Left);
        DestroyWoundableIfPresent(entMan, bodySystem, woundSystem, mob, BodyPartType.Arm, BodyPartSymmetry.Right);
        DestroyWoundableIfPresent(entMan, bodySystem, woundSystem, mob, BodyPartType.Leg, BodyPartSymmetry.Left);
        DestroyWoundableIfPresent(entMan, bodySystem, woundSystem, mob, BodyPartType.Leg, BodyPartSymmetry.Right);

        if (!bodySystem.BodyHasArachneOrgan(mob))
            return;

        foreach (var woundable in bodySystem.GetWoundableTargets(mob).ToList())
        {
            if (!entMan.TryGetComponent(woundable, out OrganComponent? organ) || organ.Category is not { } category)
                continue;

            if (!SurgeryBodyPartMapping.IsSpiderLegCategory(category))
                continue;

            if (!entMan.TryGetComponent(woundable, out WoundableComponent? woundableComp))
                continue;

            woundSystem.DestroyWoundable(mob, woundable, woundableComp);
        }
    }

    private static void SelectiveGibExceptTorsoAndHead(
        IEntityManager entMan,
        BkmBodySystem bodySystem,
        WoundSystem woundSystem,
        EntityUid mob,
        ProtoId<OrganCategoryPrototype> torsoCategory,
        ProtoId<OrganCategoryPrototype> headCategory)
    {
        foreach (var woundable in bodySystem.GetWoundableTargets(mob).ToList())
        {
            if (!entMan.TryGetComponent(woundable, out OrganComponent? organ))
                continue;

            if (organ.Category == torsoCategory || organ.Category == headCategory)
                continue;

            if (!entMan.TryGetComponent(woundable, out WoundableComponent? woundableComp))
                continue;

            woundSystem.DestroyWoundable(mob, woundable, woundableComp);
        }
    }

    private static void DestroyWoundableIfPresent(
        IEntityManager entMan,
        BkmBodySystem bodySystem,
        WoundSystem woundSystem,
        EntityUid mob,
        BodyPartType partType,
        BodyPartSymmetry? symmetry)
    {
        if (!bodySystem.TryGetWoundableTargetByType(mob, partType, symmetry, out var woundable)
            || !entMan.TryGetComponent(woundable, out WoundableComponent? woundableComp))
            return;

        woundSystem.DestroyWoundable(mob, woundable, woundableComp);
    }
}
