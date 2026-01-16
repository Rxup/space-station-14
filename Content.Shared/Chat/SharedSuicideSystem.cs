using System.Linq;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs.Components;
using Robust.Shared.Prototypes;
using Content.Shared.Backmen.Surgery.Consciousness;
using Content.Shared.Backmen.Surgery.Consciousness.Components;
using Content.Shared.Backmen.Surgery.Consciousness.Systems;
using Content.Shared.Backmen.Targeting;
using Content.Shared.Body.Components;

namespace Content.Shared.Chat;

public sealed class SharedSuicideSystem : EntitySystem
{
    private static readonly ProtoId<DamageTypePrototype> FallbackDamageType = "Blunt";

    [Dependency] private readonly DamageableSystem _damageableSystem = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly ConsciousnessSystem _consciousness = default!;

    /// <summary>
    /// Applies lethal damage spread out across the damage types given.
    /// </summary>
    public void ApplyLethalDamage(Entity<DamageableComponent> target, DamageSpecifier damageSpecifier)
    {
        // backmen edit start
        if (TryComp<ConsciousnessComponent>(target, out var victimConsciousness))
        {
            KillConsciousness((target, victimConsciousness));
            return;
        }
        // backmen edit end

        // Create a new damageSpecifier so that we don't make alterations to the original DamageSpecifier
        // Failing  to do this will permanently change a weapon's damage making it insta-kill people
        var appliedDamageSpecifier = new DamageSpecifier(damageSpecifier);
        if (!TryComp<MobThresholdsComponent>(target, out var mobThresholds))
            return;

        // Mob thresholds are sorted from alive -> crit -> dead,
        // grabbing the last key will give us how much damage is needed to kill a target from zero
        // The exact lethal damage amount is adjusted based on their current damage taken
        var lethalAmountOfDamage = mobThresholds.Thresholds.Keys.Last() - target.Comp.TotalDamage;
        var totalDamage = appliedDamageSpecifier.GetTotal();

        // Removing structural because it causes issues against entities that cannot take structural damage,
        // then getting the total to use in calculations for spreading out damage.
        appliedDamageSpecifier.DamageDict.Remove("Structural");

        // Split the total amount of damage needed to kill the target by every damage type in the DamageSpecifier
        foreach (var (key, value) in appliedDamageSpecifier.DamageDict)
        {
            appliedDamageSpecifier.DamageDict[key] = Math.Ceiling((double) (value * lethalAmountOfDamage / totalDamage));
        }

        _damageableSystem.ChangeDamage(target.AsNullable(), appliedDamageSpecifier, true, origin: target, targetPart: TargetBodyPart.Head); // backmen
    }

    /// <summary>
    /// Applies lethal damage in a single type, specified by a single damage type.
    /// </summary>
    public void ApplyLethalDamage(Entity<DamageableComponent> target, ProtoId<DamageTypePrototype>? damageType)
    {
        // backmen edit start
        if (TryComp<ConsciousnessComponent>(target, out var victimConsciousness))
        {
            // redirect suicide to consciousness
            KillConsciousness((target, victimConsciousness));
            return;
        }
        // backmen edit end

        if (!TryComp<MobThresholdsComponent>(target, out var mobThresholds))
            return;

        // Mob thresholds are sorted from alive -> crit -> dead,
        // grabbing the last key will give us how much damage is needed to kill a target from zero
        // The exact lethal damage amount is adjusted based on their current damage taken
        var lethalAmountOfDamage = mobThresholds.Thresholds.Keys.Last() - target.Comp.TotalDamage;

        // We don't want structural damage for the same reasons listed above
        if (!_prototypeManager.TryIndex(damageType, out var damagePrototype) || damagePrototype.ID == "Structural")
        {
            Log.Error($"{nameof(SharedSuicideSystem)} could not find the damage type prototype associated with {damageType}. Falling back to {FallbackDamageType}");
            damagePrototype = _prototypeManager.Index(FallbackDamageType);
        }

        var damage = new DamageSpecifier(damagePrototype, lethalAmountOfDamage);
        _damageableSystem.ChangeDamage(target.AsNullable(), damage, true, origin: target, targetPart: TargetBodyPart.Head); // backmen
    }

    /// <summary>
    /// kills a consciousness. lol.
    /// backmen edit
    /// </summary>
    public void KillConsciousness(Entity<ConsciousnessComponent> target)
    {
        _consciousness.ClearForceEffects(target.AsNullable());

        // Start-backmen: create copies of keys to avoid InvalidOperationException when modifying collection during iteration
        var modifierKeys = target.Comp.Modifiers.Keys.ToList();
        foreach (var (k1,k2) in modifierKeys)
        {
            _consciousness.RemoveConsciousnessModifier(target.AsNullable(), k1, k2);
        }

        var multiplierKeys = target.Comp.Multipliers.Keys.ToList();
        foreach (var (k1,k2) in multiplierKeys)
        {
            _consciousness.RemoveConsciousnessMultiplier(target.AsNullable(), k1, k2);
        }
        // End-backmen: create copies of keys to avoid InvalidOperationException

        _consciousness.AddConsciousnessModifier(target.AsNullable(), target, -target.Comp.Cap, "Suicide", ConsciousnessModType.Pain);
        _consciousness.AddConsciousnessMultiplier(target.AsNullable(), target, 0f, "Suicide", ConsciousnessModType.Pain);
    }
}
