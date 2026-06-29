using System.Collections.Generic;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.IntegrationTests.Utility;
using Content.Shared.Backmen.Body.OrganRelations;
using Content.Shared.Backmen.Surgery.Consciousness.Components;
using Content.Shared.Backmen.Surgery.Wounds.Components;
using Content.Shared.Backmen.Targeting;
using Content.Shared.Body;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.Damageable;

[TestFixture]
[TestOf(typeof(DamageableComponent))]
[TestOf(typeof(DamageableSystem))]
public sealed class DamageAllPrototypesTest : GameTest
{
    [SidedDependency(Side.Server)] private readonly DamageableSystem _damageableSystem = default!;

    private static string[] _damageables = GameDataScrounger.EntitiesWithComponent("Damageable");

    /// <summary>
    /// Runs every damageable prototype in one pool session and reports all failures at once.
    /// Run via <c>Tools/check-damageable-prototypes.ps1</c>.
    /// </summary>
    [Test]
    public async Task TestAllDamageableComponentsReport()
    {
        var map = await Pair.CreateTestMap();
        var fails = new List<string>();

        foreach (var damageable in _damageables)
        {
            var entity = await SpawnAtPosition(damageable, map.GridCoords);

            if (NeedsBodyAssemblyWait(SEntMan, entity))
                await Server.WaitRunTicks(2);

            await Server.WaitPost(() =>
            {
                if (ShouldSkipDamageableCheck(SEntMan, entity, _damageableSystem))
                {
                    SDeleteNow(entity);
                    return;
                }

                var origin = entity;
                var targetPart = GetDamageTargetPart(SEntMan, entity);
                var canBeDamaged = false;

                foreach (var type in SProtoMan.EnumeratePrototypes<DamageTypePrototype>())
                {
                    if (!_damageableSystem.CanBeDamagedBy(entity, type))
                        continue;

                    canBeDamaged = true;

                    var damage = new DamageSpecifier(type, FixedPoint2.Epsilon);
                    var previousDamage = _damageableSystem.GetTotalDamage(entity);
                    // AggressiveComponent rejects null origins on non-player mobs.
                    _damageableSystem.ChangeDamage(
                        entity,
                        damage,
                        ignoreResistances: true,
                        origin: origin,
                        targetPart: targetPart);
                    var newDamage = _damageableSystem.GetTotalDamage(entity);

                    if (newDamage != FixedPoint2.Epsilon + previousDamage)
                    {
                        fails.Add(
                            $"{damageable}: ChangeDamage({type}) expected {FixedPoint2.Epsilon + previousDamage}, got {newDamage}");
                    }

                    _damageableSystem.ClearAllDamage(entity);
                }

                if (!canBeDamaged && HasDamageModel(SEntMan, entity))
                    fails.Add($"{damageable}: CanBeDamagedBy returned false for all damage types");

                SDeleteNow(entity);
            });
        }

        if (fails.Count > 0)
            Assert.Fail(string.Join("\n", fails));
    }

    [Test]
    [TestOf(typeof(DamageableSystem))]
    [TestCaseSource(nameof(_damageables))]
    [Description("Ensures all Entity Prototypes with damageable can be damaged.")]
    public async Task TestDamageableComponents(string damageable)
    {
        var map = await Pair.CreateTestMap();

        var entity = await SpawnAtPosition(damageable, map.GridCoords);
        var skipped = false;
        var canBeDamaged = false;

        if (NeedsBodyAssemblyWait(SEntMan, entity))
            await Server.WaitRunTicks(2);

        await Server.WaitPost(() =>
        {
            if (ShouldSkipDamageableCheck(SEntMan, entity, _damageableSystem))
            {
                skipped = true;
                return;
            }

            var origin = entity;
            var targetPart = GetDamageTargetPart(SEntMan, entity);

            foreach (var type in SProtoMan.EnumeratePrototypes<DamageTypePrototype>())
            {
                if (!_damageableSystem.CanBeDamagedBy(entity, type))
                    continue;

                canBeDamaged = true;

                var damage = new DamageSpecifier(type, FixedPoint2.Epsilon);
                var previousDamage = _damageableSystem.GetTotalDamage(entity);
                _damageableSystem.ChangeDamage(
                    entity,
                    damage,
                    ignoreResistances: true,
                    origin: origin,
                    targetPart: targetPart);
                Assert.That(_damageableSystem.GetTotalDamage(entity) == FixedPoint2.Epsilon + previousDamage);
                _damageableSystem.ClearAllDamage(entity);
            }
        });

        if (skipped)
            return;

        // Ensure that this entity can actually be damaged.
        Assert.That(canBeDamaged);
    }

    private static bool ShouldSkipDamageableCheck(
        IEntityManager entMan,
        EntityUid entity,
        DamageableSystem damageableSystem)
    {
        // Bundle shells proxy damage to contained organs; Damageable.Damage on the shell stays zero.
        if (entMan.HasComponent<GodmodeComponent>(entity)
            || entMan.HasComponent<BkmDetachedBodyComponent>(entity))
            return true;

        // Prototype has Damageable but no configured damage model (e.g. legacy monkey mobs).
        if (!HasDamageModel(entMan, entity))
            return true;

        // Pre-damaged prototypes (corpses, etc.) may already be at wound severity cap.
        return damageableSystem.GetTotalDamage(entity) > FixedPoint2.Zero;
    }

    private static bool HasDamageModel(IEntityManager entMan, EntityUid entity) =>
        entMan.HasComponent<InjurableComponent>(entity)
        || entMan.HasComponent<WoundableComponent>(entity)
        || entMan.HasComponent<ConsciousnessComponent>(entity)
        || entMan.HasComponent<BkmDetachedBodyComponent>(entity);

    /// <summary>
    /// Surgery mobs assemble woundable body parts after spawn; damage routing needs them present.
    /// </summary>
    private static bool NeedsBodyAssemblyWait(IEntityManager entMan, EntityUid entity) =>
        entMan.HasComponent<ConsciousnessComponent>(entity)
        || entMan.HasComponent<BodyComponent>(entity);

    /// <summary>
    /// Consciousness routes through a single external woundable unless a caller specifies all parts.
    /// </summary>
    private static TargetBodyPart? GetDamageTargetPart(IEntityManager entMan, EntityUid entity) =>
        entMan.HasComponent<ConsciousnessComponent>(entity) ? TargetBodyPart.Head : null;
}
