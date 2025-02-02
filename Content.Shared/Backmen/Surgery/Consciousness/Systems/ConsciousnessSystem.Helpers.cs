using System.Linq;
using Content.Shared.Backmen.Surgery.Consciousness.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;

namespace Content.Shared.Backmen.Surgery.Consciousness.Systems;

public partial class ConsciousnessSystem
{

    #region PublicApi

    /// <summary>
    /// Checks to see if an entity should be made unconscious, this is called whenever any consciousness values are changed.
    /// Unless you are directly modifying a consciousness component (pls dont) you don't need to call this.
    /// </summary>
    /// <param name="target">Target entity</param>
    /// <param name="consciousness">Consciousness component</param>
    public bool CheckConscious(EntityUid target, ConsciousnessComponent? consciousness = null)
    {
        if (!Resolve(target, ref consciousness))
            return false;

        SetConscious(target, consciousness.Consciousness > consciousness.Threshold, consciousness);
        UpdateMobState(target, consciousness);

        return consciousness.Consciousness > consciousness.Threshold;
    }

    /// <summary>
    /// Force passes out an entity with consciousness component.
    /// </summary>
    /// <param name="target">Target to pass out.</param>
    /// <param name="time">Time. In seconds.</param>
    /// <param name="consciousness"><see cref="ConsciousnessComponent"/> of an entity.</param>
    public void ForcePassout(EntityUid target, TimeSpan time, ConsciousnessComponent? consciousness = null)
    {
        if (!Resolve(target, ref consciousness))
            return;

        consciousness.PassedOut = true;
        consciousness.PassedOutTime = _timing.CurTime + time;

        CheckConscious(target, consciousness);
    }

    #endregion

    #region Private Implementation

    private void UpdateConsciousnessModifiers(EntityUid uid, ConsciousnessComponent? consciousness)
    {
        if (!Resolve(uid, ref consciousness))
            return;

        var totalDamage
            = consciousness.Modifiers.Aggregate((FixedPoint2) 0,
                (current, modifier) => current + modifier.Value.Change * consciousness.Multiplier);

        consciousness.RawConsciousness = consciousness.Cap + totalDamage;

        Dirty(uid, consciousness);
    }

    private void UpdateConsciousnessMultipliers(EntityUid uid, ConsciousnessComponent? consciousness)
    {
        if (!Resolve(uid, ref consciousness))
            return;

        consciousness.Multiplier = consciousness.Multipliers.Aggregate((FixedPoint2) 0,
            (current, multiplier) => current + multiplier.Value.Change) / consciousness.Multipliers.Count;

        UpdateConsciousnessModifiers(uid, consciousness);
    }

    /// <summary>
    /// Only used internally. Do not use this, instead use consciousness modifiers/multipliers!
    /// </summary>
    /// <param name="target">target entity</param>
    /// <param name="isConscious">should this entity be conscious</param>
    /// <param name="consciousness">consciousness component</param>
    private void SetConscious(EntityUid target, bool isConscious, ConsciousnessComponent? consciousness = null)
    {
        if (!Resolve(target,ref consciousness) || _net.IsClient)
            return;

        consciousness.IsConscious = isConscious;

        Dirty(target, consciousness);
    }

    private void UpdateMobState(EntityUid target, ConsciousnessComponent? consciousness = null, MobStateComponent? mobState = null)
    {
        if (!Resolve(target, ref consciousness, ref mobState) || _net.IsClient || TerminatingOrDeleted(target))
            return;

        var newMobState = consciousness.IsConscious
            ? MobState.Alive
            : MobState.Critical;

        if (consciousness.PassedOut)
            newMobState = MobState.Critical;

        if (consciousness.ForceUnconscious)
            newMobState = MobState.Critical;

        if (consciousness.Consciousness <= 0)
            newMobState = MobState.Dead;

        if (consciousness.ForceDead)
            newMobState = MobState.Dead;

        _mobStateSystem.ChangeMobState(target, newMobState, mobState);
    }

    private void CheckRequiredParts(EntityUid bodyId, ConsciousnessComponent consciousness)
    {
        var alive = true;
        var conscious = true;

        foreach (var (/*identifier */_, (entity, forcesDeath, isLost)) in consciousness.RequiredConsciousnessParts)
        {
            if (entity == null || !isLost)
                continue;

            if (forcesDeath)
            {
                consciousness.ForceDead = true;
                Dirty(bodyId, consciousness);

                alive = false;
                break;
            }

            conscious = false;
        }

        if (alive)
        {
            consciousness.ForceDead = false;
            consciousness.ForceUnconscious = !conscious;

            Dirty(bodyId, consciousness);
        }

        CheckConscious(bodyId, consciousness);
    }

    #endregion

    #region Multipliers and Modifiers


    /// <summary>
    /// Get all consciousness multipliers present on an entity. Note: these are copies, do not try to edit the values
    /// </summary>
    /// <param name="target">target entity</param>
    /// <param name="consciousness">consciousness component</param>
    /// <returns>Enumerable of Modifiers</returns>
    public IEnumerable<((EntityUid, ConsciousnessModType), ConsciousnessModifier)> GetAllModifiers(EntityUid target,
        ConsciousnessComponent? consciousness = null)
    {
        if (!Resolve(target, ref consciousness))
            yield break;

        foreach (var (owner, modifier) in consciousness.Modifiers)
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
    public IEnumerable<((EntityUid, ConsciousnessModType), ConsciousnessMultiplier)> GetAllMultipliers(EntityUid target,
        ConsciousnessComponent? consciousness = null)
    {
        if (!Resolve(target, ref consciousness))
            yield break;

        foreach (var (owner, multiplier) in consciousness.Multipliers)
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
    /// <returns>Successful</returns>
    public bool AddConsciousnessModifier(EntityUid target,
        EntityUid modifierOwner,
        FixedPoint2 modifier,
        ConsciousnessComponent? consciousness = null,
        string identifier = UnspecifiedIdentifier,
        ConsciousnessModType type = ConsciousnessModType.Generic)
    {
        if (!Resolve(target, ref consciousness) || modifier == 0 ||  _net.IsClient)
            return false;

        if (!consciousness.Modifiers.TryAdd((modifierOwner, type), new ConsciousnessModifier(modifier, identifier)))
            return false;

        UpdateConsciousnessModifiers(target, consciousness);

        var ev = new ConsciousnessUpdatedEvent(CheckConscious(target, consciousness), modifier * consciousness.Multiplier);
        RaiseLocalEvent(target, ref ev, true);

        Dirty(target, consciousness);

        return true;
    }

    /// <summary>
    /// Get a copy of a consciousness modifier. This value gets added to the raw consciousness value.
    /// </summary>
    /// <param name="target">Target entity</param>
    /// <param name="modifierOwner">Owner of a modifier</param>
    /// <param name="modifier">copy of the found modifier, changes are NOT saved</param>
    /// <param name="consciousness">Consciousness component</param>
    /// <param name="type">Modifier type, defaults to generic</param>
    /// <returns>Successful</returns>
    public bool TryGetConsciousnessModifier(EntityUid target,
        EntityUid modifierOwner,
        out ConsciousnessModifier? modifier,
        ConsciousnessComponent? consciousness = null,
        ConsciousnessModType type = ConsciousnessModType.Generic)
    {
        modifier = null;
        if (_net.IsClient)
            return false;

        if (!Resolve(target, ref consciousness) ||
            !consciousness.Modifiers.TryGetValue((modifierOwner,type), out var rawModifier))
            return false;

        modifier = rawModifier;

        return true;
    }

    /// <summary>
    /// Remove a consciousness modifier. This value gets added to the raw consciousness value.
    /// </summary>
    /// <param name="target">Target entity</param>
    /// <param name="modifierOwner">Owner of a modifier</param>
    /// <param name="consciousness">Consciousness component</param>
    /// <param name="type">Modifier type, defaults to generic</param>
    /// <returns>Successful</returns>
    public bool RemoveConsciousnessModifer(EntityUid target,
        EntityUid modifierOwner,
        ConsciousnessComponent? consciousness = null,
        ConsciousnessModType type = ConsciousnessModType.Generic)
    {
        if (!Resolve(target, ref consciousness) || _net.IsClient)
            return false;

        if (!consciousness.Modifiers.Remove((modifierOwner,type), out var foundModifier))
            return false;

        UpdateConsciousnessModifiers(target, consciousness);

        var ev = new ConsciousnessUpdatedEvent(CheckConscious(target, consciousness),
            foundModifier.Change * consciousness.Multiplier);
        RaiseLocalEvent(target, ref ev, true);

        Dirty(target, consciousness);

        return true;
    }

    /// <summary>
    /// Edit a consciousness modifier. This value gets set to the raw consciousness value.
    /// </summary>
    /// <param name="target">Target entity</param>
    /// <param name="modifierOwner">Owner of a modifier</param>
    /// <param name="modifierChange">Value that is being added onto the modifier</param>
    /// <param name="consciousness">Consciousness component</param>
    /// <param name="type">Modifier type, defaults to generic</param>
    /// <returns>Successful</returns>
    public bool SetConsciousnessModifier(EntityUid target,
        EntityUid modifierOwner,
        FixedPoint2 modifierChange,
        ConsciousnessComponent? consciousness = null,
        ConsciousnessModType type = ConsciousnessModType.Generic)
    {
        if (!Resolve(target, ref consciousness) || _net.IsClient ||
            !consciousness.Modifiers.TryGetValue((modifierOwner,type), out var oldModifier))
            return false;

        var newModifier = oldModifier with {Change = modifierChange};

        consciousness.Modifiers[(modifierOwner,type)] = newModifier;
        UpdateConsciousnessModifiers(target, consciousness);

        var ev = new ConsciousnessUpdatedEvent(CheckConscious(target, consciousness),
            modifierChange * consciousness.Multiplier);
        RaiseLocalEvent(target, ref ev, true);

        Dirty(target, consciousness);

        return true;
    }

    /// <summary>
    /// Edit a consciousness modifier. This value gets added to the raw consciousness value.
    /// </summary>
    /// <param name="target">Target entity</param>
    /// <param name="modifierOwner">Owner of a modifier</param>
    /// <param name="modifierChange">Value that is being added onto the modifier</param>
    /// <param name="consciousness">Consciousness component</param>
    /// <param name="type">Modifier type, defaults to generic</param>
    /// <returns>Successful</returns>
    public bool EditConsciousnessModifier(EntityUid target,
        EntityUid modifierOwner,
        FixedPoint2 modifierChange,
        ConsciousnessComponent? consciousness = null,
        ConsciousnessModType type = ConsciousnessModType.Generic)
    {
        if (!Resolve(target, ref consciousness) || _net.IsClient ||
            !consciousness.Modifiers.TryGetValue((modifierOwner,type), out var oldModifier))
            return false;

        var newModifier = oldModifier with {Change = oldModifier.Change + modifierChange};

        consciousness.Modifiers[(modifierOwner,type)] = newModifier;
        UpdateConsciousnessModifiers(target, consciousness);

        var ev = new ConsciousnessUpdatedEvent(CheckConscious(target, consciousness),
            modifierChange * consciousness.Multiplier);
        RaiseLocalEvent(target, ref ev, true);

        Dirty(target, consciousness);

        return true;
    }

    /// <summary>
    /// Update the identifier string for a consciousness modifier
    /// </summary>
    /// <param name="target">Target entity</param>
    /// <param name="modifierOwner">Owner of a modifier</param>
    /// <param name="newIdentifier">New localized string to identify this modifier</param>
    /// <param name="consciousness">Consciousness component</param>
    /// <param name="type">Modifier type, defaults to generic</param>
    /// <returns>Successful</returns>
    public bool UpdateConsciousnessModifierMetaData(EntityUid target,
        EntityUid modifierOwner,
        string newIdentifier,
        ConsciousnessComponent? consciousness = null,
        ConsciousnessModType type = ConsciousnessModType.Generic)
    {
        if (!Resolve(target, ref consciousness) || _net.IsClient ||
            !consciousness.Modifiers.TryGetValue((modifierOwner,type), out var oldMultiplier))
            return false;

        var newMultiplier = oldMultiplier with {Identifier = newIdentifier};

        consciousness.Modifiers[(modifierOwner, type)] = newMultiplier;

        //TODO: create/raise an identifier changed event if needed
        Dirty(target, consciousness);
        return true;
    }

    /// <summary>
    /// Add a unique consciousness multiplier. This value gets added onto the multiplier used to calculate consciousness.
    /// The owner and type combo must be unique, if you are adding multiple values from a single owner and type, combine them into one multiplier
    /// </summary>
    /// <param name="target">Target entity</param>
    /// <param name="multiplierOwner">Owner of a multiplier</param>
    /// <param name="multiplier">Value of the multiplier</param>
    /// <param name="consciousness">ConsciousnessComponent</param>
    /// <param name="identifier">Localized text name for the multiplier (for debug/admins)</param>
    /// <param name="type">Multiplier type, defaults to generic</param>
    /// <returns>Successful</returns>
    public bool AddConsciousnessMultiplier(EntityUid target,
        EntityUid multiplierOwner,
        FixedPoint2 multiplier,
        ConsciousnessComponent? consciousness = null,
        string identifier = UnspecifiedIdentifier,
        ConsciousnessModType type = ConsciousnessModType.Generic)
    {
        if (!Resolve(target, ref consciousness) || multiplier == 0 || _net.IsClient)
            return false;

        if (!consciousness.Multipliers.TryAdd((multiplierOwner,type), new ConsciousnessMultiplier(multiplier, identifier)))
            return false;

        UpdateConsciousnessMultipliers(target, consciousness);

        var ev = new ConsciousnessUpdatedEvent(CheckConscious(target, consciousness), multiplier * consciousness.RawConsciousness);
        RaiseLocalEvent(target, ref ev, true);

        Dirty(target, consciousness);

        return true;
    }

    /// <summary>
    /// Get a copy of a consciousness multiplier. This value gets added onto the multiplier used to calculate consciousness.
    /// </summary>
    /// <param name="target">Target entity</param>
    /// <param name="multiplierOwner">Owner of a multiplier</param>
    /// <param name="multiplier">Copy of the found multiplier, changes are NOT saved</param>
    /// <param name="consciousness">Consciousness component</param>
    /// <param name="type">Multiplier type, defaults to generic</param>
    /// <returns>Successful</returns>
    public bool TryGetConsciousnessMultiplier(EntityUid target,
        EntityUid multiplierOwner,
        out ConsciousnessMultiplier? multiplier,
        ConsciousnessComponent? consciousness = null,
        ConsciousnessModType type = ConsciousnessModType.Generic)
    {
        multiplier = null;
        if (_net.IsClient)
            return false;

        if (!Resolve(target, ref consciousness) ||
            !consciousness.Multipliers.TryGetValue((multiplierOwner, type), out var rawMultiplier))
            return false;

        multiplier = rawMultiplier;

        return true;
    }

    /// <summary>
    /// Remove a consciousness multiplier. This value gets added onto the multiplier used to calculate consciousness.
    /// </summary>
    /// <param name="target">Target entity</param>
    /// <param name="multiplierOwner">Owner of a multiplier</param>
    /// <param name="type">Multiplier type, defaults to generic</param>
    /// <param name="consciousness">Consciousness component</param>
    /// <returns>Successful</returns>
    public bool RemoveConsciousnessMultiplier(EntityUid target,
        EntityUid multiplierOwner,
        ConsciousnessModType type = ConsciousnessModType.Generic,
        ConsciousnessComponent? consciousness = null)
    {
        if (!Resolve(target, ref consciousness) || _net.IsClient)
            return false;

        if (!consciousness.Multipliers.Remove((multiplierOwner, type), out var foundMultiplier))
            return false;

        UpdateConsciousnessMultipliers(target, consciousness);

        var ev = new ConsciousnessUpdatedEvent(CheckConscious(target, consciousness),
            foundMultiplier.Change * consciousness.RawConsciousness);
        RaiseLocalEvent(target, ref ev, true);

        Dirty(target, consciousness);
        UpdateConsciousnessModifiers(target, consciousness);

        return true;
    }

    /// <summary>
    /// Edit a consciousness multiplier. This value gets added onto the multiplier used to calculate consciousness.
    /// </summary>
    /// <param name="target">Target entity</param>
    /// <param name="multiplierOwner">Owner of a multiplier</param>
    /// <param name="multiplierChange">Value that is being added onto the multiplier</param>
    /// <param name="type">Multiplier type, defaults to generic</param>
    /// <param name="consciousness">Consciousness component</param>
    /// <returns>Successful</returns>
    public bool EditConsciousnessMultiplier(EntityUid target,
        EntityUid multiplierOwner,
        FixedPoint2 multiplierChange,
        ConsciousnessComponent? consciousness = null,
        ConsciousnessModType type = ConsciousnessModType.Generic)
    {
        if (!Resolve(target, ref consciousness) || _net.IsClient ||
            !consciousness.Multipliers.TryGetValue((multiplierOwner, type), out var oldMultiplier))
            return false;

        var newMultiplier = oldMultiplier with {Change = oldMultiplier.Change + multiplierChange};

        consciousness.Multipliers[(multiplierOwner, type)] = newMultiplier;
        UpdateConsciousnessMultipliers(target, consciousness);

        var ev = new ConsciousnessUpdatedEvent(CheckConscious(target, consciousness),
            multiplierChange * consciousness.RawConsciousness);
        RaiseLocalEvent(target, ref ev, true);

        Dirty(target, consciousness);
        UpdateConsciousnessModifiers(target, consciousness);

        return true;
    }

    /// <summary>
    /// Update the identifier string for a consciousness multiplier
    /// </summary>
    /// <param name="target">Target entity</param>
    /// <param name="multiplierOwner">Owner of a multiplier</param>
    /// <param name="newIdentifier">New localized string to identify this multiplier</param>
    /// <param name="type">Multiplier type, defaults to generic</param>
    /// <param name="consciousness">Consciousness component</param>
    /// <returns>Successful</returns>
    public bool UpdateConsciousnessMultiplierMetaData(EntityUid target,
        EntityUid multiplierOwner,
        string newIdentifier,
        ConsciousnessComponent? consciousness = null,
        ConsciousnessModType type = ConsciousnessModType.Generic)
    {
        if (!Resolve(target, ref consciousness) || _net.IsClient ||
            !consciousness.Multipliers.TryGetValue((multiplierOwner, type), out var oldMultiplier))
            return false;

        var newMultiplier = oldMultiplier with {Identifier = newIdentifier};

        consciousness.Multipliers[(multiplierOwner, type)] = newMultiplier;

        //TODO: create/raise an identifier changed event if needed

        Dirty(target, consciousness);

        return true;
    }

    #endregion
}
