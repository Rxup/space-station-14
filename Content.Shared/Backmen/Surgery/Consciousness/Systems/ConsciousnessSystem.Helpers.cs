using System.Diagnostics.CodeAnalysis;
using Content.Shared.Backmen.Surgery.Consciousness.Components;
using Content.Shared.Backmen.Surgery.Pain.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Components;
using JetBrains.Annotations;

namespace Content.Shared.Backmen.Surgery.Consciousness.Systems;

public partial class ConsciousnessSystem
{

    #region PublicApi

    /// <summary>
    /// Gets a nerve system off a body, if it has one.
    /// </summary>
    /// <param name="body">Target entity</param>
    /// <param name="consciousness">Consciousness component</param>
    [PublicAPI]
    public Entity<NerveSystemComponent>? GetNerveSystem(EntityUid body, ConsciousnessComponent? consciousness = null)
    {
        return !ConsciousnessQuery.Resolve(body, ref consciousness, false) ? null : consciousness.NerveSystem;
    }

    /// <summary>
    /// Gets a nerve system off a body, if it has one.
    /// </summary>
    /// <param name="body">Target entity</param>
    /// <param name="nerveSys">The nerve system you wanted.</param>
    /// <param name="consciousness">Consciousness component for this thingy</param>
    [PublicAPI]
    public bool TryGetNerveSystem(
        Entity<ConsciousnessComponent?> body,
        [NotNullWhen(true)] out Entity<NerveSystemComponent>? nerveSys)
    {
        nerveSys = null;
        if (!ConsciousnessQuery.Resolve(body, ref body.Comp, false))
            return false;

        if (!body.Comp.NerveSystem.HasValue)
            return false;

        nerveSys = body.Comp.NerveSystem;
        return true;
    }

    /// <summary>
    /// Checks to see if an entity should be made unconscious, this is called whenever any consciousness values are changed.
    /// Unless you are directly modifying a consciousness component (pls dont) you don't need to call this.
    /// </summary>
    /// <param name="target">Target entity</param>
    [PublicAPI]
    public virtual bool CheckConscious(
        Entity<ConsciousnessComponent?, MobStateComponent?> target)
    {
        return ConsciousnessQuery.Resolve(target, ref target.Comp1, false) && target.Comp1.IsConscious;
    }

    /// <summary>
    /// Force passes out an entity with consciousness component.
    /// </summary>
    /// <param name="target">Target to pass out.</param>
    /// <param name="time">Time.</param>
    /// <param name="consciousness"><see cref="ConsciousnessComponent"/> of an entity.</param>
    [PublicAPI]
    public virtual void ForcePassOut(
        Entity<ConsciousnessComponent?> target,
        TimeSpan time)
    {
        // Server-only execution
    }

    /// <summary>
    /// Forces the entity to stay alive even if on 0 Consciousness, unless induced injuries that cause direct death, like getting your brain blown out
    /// Overrides ForcePassout and all other factors, the only requirement is entity being able to live
    /// </summary>
    /// <param name="target">Target to pass out.</param>
    /// <param name="time">Time.</param>
    /// <param name="consciousness"><see cref="ConsciousnessComponent"/> of an entity.</param>
    [PublicAPI]
    public virtual void ForceConscious(
        Entity<ConsciousnessComponent?> target,
        TimeSpan time)
    {
        // Server-only execution
    }

    /// <summary>
    /// Removes all the forced effects like, <see cref="ForceConscious"/> or <see cref="ForcePassOut"/> and etc.
    /// </summary>
    /// <param name="target">Target that has a <see cref="ConsciousnessComponent"/></param>
    [PublicAPI]
    public virtual void ClearForceEffects(
        Entity<ConsciousnessComponent?> target)
    {
        // Server-only execution
    }

    #endregion

    #region Multipliers and Modifiers

    /// <summary>
    /// Get all consciousness multipliers present on an entity. Note: these are copies, do not try to edit the values
    /// </summary>
    /// <param name="target">target entity</param>
    /// <returns>Enumerable of Modifiers</returns>
    [PublicAPI]
    public IEnumerable<((EntityUid, string), ConsciousnessModifier)> GetAllModifiers(
        Entity<ConsciousnessComponent?> target)
    {
        if (!ConsciousnessQuery.Resolve(target, ref target.Comp, false))
            yield break;

        foreach (var (owner, modifier) in target.Comp.Modifiers)
        {
            yield return (owner, modifier);
        }
    }

    /// <summary>
    /// Get all consciousness multipliers present on an entity. Note: these are copies, do not try to edit the values
    /// </summary>
    /// <param name="target">target entity</param>
    /// <param name="consciousness">consciousness component</param>
    /// <returns>Enumerable of Multipliers</returns>
    [PublicAPI]
    public IEnumerable<((EntityUid, string), ConsciousnessMultiplier)> GetAllMultipliers(
        Entity<ConsciousnessComponent?> target)
    {
        if (!ConsciousnessQuery.Resolve(target, ref target.Comp, false))
            yield break;

        foreach (var (owner, multiplier) in target.Comp.Multipliers)
        {
            yield return (owner, multiplier);
        }
    }

    /// <summary>
    /// Add a unique consciousness modifier. This value gets added to the raw consciousness value.
    /// The owner and type combo must be unique, if you are adding multiple values from a single owner and type, combine them into one modifier
    /// </summary>
    /// <param name="target">Target entity</param>
    /// <param name="modifierOwner">Owner of a modifier</param>
    /// <param name="modifier">Value of the modifier</param>
    /// <param name="consciousness">ConsciousnessComponent</param>
    /// <param name="identifier">Localized text name for the modifier (for debug/admins)</param>
    /// <param name="type">Modifier type, defaults to generic</param>
    /// <param name="time">Time spawn for which the consciousness modifier will exist</param>
    /// <returns>Successful</returns>
    [PublicAPI]
    public virtual bool AddConsciousnessModifier(Entity<ConsciousnessComponent?> target,
        EntityUid modifierOwner,
        FixedPoint2 modifier,
        string identifier = "Unspecified",
        ConsciousnessModType type = ConsciousnessModType.Generic,
        TimeSpan? time = null)
    {
        // Server-only execution
        return false;
    }

    /// <summary>
    /// Get a copy of a consciousness modifier. This value gets added to the raw consciousness value.
    /// </summary>
    /// <param name="target">Target entity</param>
    /// <param name="modifierOwner">Owner of a modifier</param>
    /// <param name="modifier">copy of the found modifier, changes are NOT saved</param>
    /// <param name="identifier">Identifier of the requested modifier</param>
    /// <param name="consciousness">Consciousness component</param>
    /// <returns>Successful</returns>
    [PublicAPI]
    public bool TryGetConsciousnessModifier(Entity<ConsciousnessComponent?> target,
        EntityUid modifierOwner,
        [NotNullWhen(true)] out ConsciousnessModifier? modifier,
        string identifier)
    {
        modifier = null;
        if (!ConsciousnessQuery.Resolve(target, ref target.Comp, false)
            || !target.Comp.Modifiers.TryGetValue((modifierOwner, identifier), out var rawModifier))
            return false;

        modifier = rawModifier;
        return true;
    }

    /// <summary>
    /// Remove a consciousness modifier. This value gets added to the raw consciousness value.
    /// </summary>
    /// <param name="target">Target entity</param>
    /// <param name="modifierOwner">Owner of a modifier</param>
    /// <param name="identifier">Identifier of the modifier to remove</param>
    /// <returns>Successful</returns>
    [PublicAPI]
    public virtual bool RemoveConsciousnessModifier(Entity<ConsciousnessComponent?> target,
        EntityUid modifierOwner,
        string identifier)
    {
        // Server-only execution
        return false;
    }

    /// <summary>
    /// Edit a consciousness modifier. This value gets set to the raw consciousness value.
    /// </summary>
    /// <param name="target">Target entity</param>
    /// <param name="modifierOwner">Owner of a modifier</param>
    /// <param name="modifierChange">Value that is being added onto the modifier</param>
    /// <param name="identifier">The string identifier of this modifier.</param>
    /// <param name="type">Modifier type, defaults to generic</param>
    /// <param name="time">Time span for which the change will exist</param>
    /// <returns>Successful</returns>
    [PublicAPI]
    public virtual bool SetConsciousnessModifier(Entity<ConsciousnessComponent?> target,
        EntityUid modifierOwner,
        FixedPoint2 modifierChange,
        string identifier = "Unspecified",
        ConsciousnessModType type = ConsciousnessModType.Generic,
        TimeSpan? time = null)
    {
        // Server-only execution
        return false;
    }

    /// <summary>
    /// Edit a consciousness modifier. This value gets added to the raw consciousness value.
    /// </summary>
    /// <param name="target">Target entity</param>
    /// <param name="modifierOwner">Owner of a modifier</param>
    /// <param name="modifierChange">Value that is being added onto the modifier</param>
    /// <param name="identifier">The string identifier of the modifier to change</param>
    /// <param name="time">Time span for which this modifier shall exist</param>
    /// <returns>Successful</returns>
    [PublicAPI]
    public virtual bool ChangeConsciousnessModifier(Entity<ConsciousnessComponent?> target,
        EntityUid modifierOwner,
        FixedPoint2 modifierChange,
        string identifier,
        TimeSpan? time = null)
    {
        // Server-only execution
        return false;
    }

    /// <summary>
    /// Add a unique consciousness multiplier. This value gets added onto the multiplier used to calculate consciousness.
    /// The owner and type combo must be unique, if you are adding multiple values from a single owner and type, combine them into one multiplier
    /// </summary>
    /// <param name="target">Target entity</param>
    /// <param name="multiplierOwner">Owner of a multiplier</param>
    /// <param name="multiplier">Value of the multiplier</param>
    /// <param name="identifier">Localized text name for the multiplier (for debug/admins)</param>
    /// <param name="type">Multiplier type, defaults to generic</param>
    /// <param name="time">Time span for which this multiplier will exist</param>
    /// <returns>Successful</returns>
    [PublicAPI]
    public virtual bool AddConsciousnessMultiplier(Entity<ConsciousnessComponent?> target,
        EntityUid multiplierOwner,
        FixedPoint2 multiplier,
        string identifier = "Unspecified",
        ConsciousnessModType type = ConsciousnessModType.Generic,
        TimeSpan? time = null)
    {
        // Server-only execution
        return false;
    }

    /// <summary>
    /// Get a copy of a consciousness multiplier. This value gets added onto the multiplier used to calculate consciousness.
    /// </summary>
    /// <param name="target">Target entity</param>
    /// <param name="multiplierOwner">Owner of a multiplier</param>
    /// <param name="identifier">String identifier of the multiplier to get</param>
    /// <param name="multiplier">Copy of the found multiplier, changes are NOT saved</param>
    /// <param name="consciousness">Consciousness component</param>
    /// <returns>Successful</returns>
    [PublicAPI]
    public bool TryGetConsciousnessMultiplier(Entity<ConsciousnessComponent?> target,
        EntityUid multiplierOwner,
        string identifier,
        out ConsciousnessMultiplier? multiplier)
    {
        multiplier = null;
        if (!Resolve(target, ref target.Comp) ||
            !target.Comp.Multipliers.TryGetValue((multiplierOwner, identifier), out var rawMultiplier))
            return false;

        multiplier = rawMultiplier;

        return true;
    }

    /// <summary>
    /// Remove a consciousness multiplier. This value gets added onto the multiplier used to calculate consciousness.
    /// </summary>
    /// <param name="target">Target entity</param>
    /// <param name="multiplierOwner">Owner of a multiplier</param>
    /// <param name="identifier">String identifier of the multiplier to remove</param>
    /// <returns>Successful</returns>
    [PublicAPI]
    public virtual bool RemoveConsciousnessMultiplier(Entity<ConsciousnessComponent?> target,
        EntityUid multiplierOwner,
        string identifier)
    {
        // Server-only execution
        return false;
    }

    #endregion
}
