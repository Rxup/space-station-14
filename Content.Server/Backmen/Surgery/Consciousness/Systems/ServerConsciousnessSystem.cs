using System.Linq;
using Content.Shared.Backmen.Surgery.Consciousness;
using Content.Shared.Backmen.Surgery.Consciousness.Components;
using Content.Shared.Backmen.Surgery.Consciousness.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Components;
using JetBrains.Annotations;

namespace Content.Server.Backmen.Surgery.Consciousness.Systems;

public sealed class ServerConsciousnessSystem : ConsciousnessSystem
{
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ConsciousnessComponent, MetaDataComponent>();
        while (query.MoveNext(out var ent, out var consciousness, out var meta))
        {
            if (Paused(ent, meta))
                continue;

            if (consciousness.ForceDead || Timing.CurTime < consciousness.NextConsciousnessUpdate)
                continue;
            consciousness.NextConsciousnessUpdate = Timing.CurTime + consciousness.ConsciousnessUpdateTime;

            foreach (var modifier in
                     consciousness.Modifiers.Where(modifier => modifier.Value.Time < Timing.CurTime))
            {
                RemoveConsciousnessModifier(ent, modifier.Key.Item1, modifier.Key.Item2, consciousness);
            }

            foreach (var multiplier in
                     consciousness.Multipliers.Where(multiplier => multiplier.Value.Time < Timing.CurTime))
            {
                RemoveConsciousnessMultiplier(ent, multiplier.Key.Item1, multiplier.Key.Item2, consciousness);
            }

            if (consciousness.PassedOutTime < Timing.CurTime && consciousness.PassedOut)
            {
                consciousness.PassedOut = false;
                CheckConscious(ent, consciousness);
            }

            if (consciousness.ForceConsciousnessTime < Timing.CurTime && consciousness.ForceConscious)
            {
                consciousness.ForceConscious = false;
                CheckConscious(ent, consciousness);
            }
        }
    }

    #region Helpers

    [PublicAPI]
    public override bool CheckConscious(
        EntityUid target,
        ConsciousnessComponent? consciousness = null,
        MobStateComponent? mobState = null)
    {
        if (!ConsciousnessQuery.Resolve(target, ref consciousness)
            || !MobStateQuery.Resolve(target, ref mobState, false))
            return false;

        var shouldBeConscious =
            consciousness.Consciousness > consciousness.Threshold || consciousness is { ForceUnconscious: false, ForceConscious: true };

        if (shouldBeConscious == consciousness.IsConscious)
            return consciousness.IsConscious;

        var ev = new ConsciousnessUpdatedEvent(shouldBeConscious);
        RaiseLocalEvent(target, ref ev);

        SetConscious(target, shouldBeConscious, consciousness);
        UpdateMobState(target, consciousness, mobState);

        return shouldBeConscious;
    }

    [PublicAPI]
    public override void ForcePassOut(
        EntityUid target,
        TimeSpan time,
        ConsciousnessComponent? consciousness = null)
    {
        if (!ConsciousnessQuery.Resolve(target, ref consciousness))
            return;

        consciousness.PassedOut = true;
        consciousness.PassedOutTime = Timing.CurTime + time;

        CheckConscious(target, consciousness);
    }

    [PublicAPI]
    public override void ForceConscious(EntityUid target,
        TimeSpan time,
        ConsciousnessComponent? consciousness = null)
    {
        if (!ConsciousnessQuery.Resolve(target, ref consciousness))
            return;

        consciousness.ForceConscious = true;
        consciousness.ForceConsciousnessTime = Timing.CurTime + time;

        CheckConscious(target, consciousness);
    }

    #endregion

    #region Modifiers and Multipliers

    [PublicAPI]
    public override bool AddConsciousnessModifier(EntityUid target,
        EntityUid modifierOwner,
        FixedPoint2 modifier,
        string identifier = "Unspecified",
        ConsciousnessModType type = ConsciousnessModType.Generic,
        TimeSpan? time = null,
        ConsciousnessComponent? consciousness = null)
    {
        if (!ConsciousnessQuery.Resolve(target, ref consciousness))
            return false;

        if (!consciousness.Modifiers.TryAdd((modifierOwner, identifier), new ConsciousnessModifier(modifier, Timing.CurTime + time, type)))
            return false;

        UpdateConsciousnessModifiers(target, consciousness);
        Dirty(target, consciousness);

        return true;
    }

    [PublicAPI]
    public override bool RemoveConsciousnessModifier(EntityUid target,
        EntityUid modifierOwner,
        string identifier,
        ConsciousnessComponent? consciousness = null)
    {
        if (!ConsciousnessQuery.Resolve(target, ref consciousness))
            return false;

        if (!consciousness.Modifiers.Remove((modifierOwner, identifier)))
            return false;

        UpdateConsciousnessModifiers(target, consciousness);
        Dirty(target, consciousness);

        return true;
    }

    [PublicAPI]
    public override bool SetConsciousnessModifier(EntityUid target,
        EntityUid modifierOwner,
        FixedPoint2 modifierChange,
        string identifier = "Unspecified",
        ConsciousnessModType type = ConsciousnessModType.Generic,
        TimeSpan? time = null,
        ConsciousnessComponent? consciousness = null)
    {
        if (!ConsciousnessQuery.Resolve(target, ref consciousness))
            return false;

        var newModifier = new ConsciousnessModifier(Change: modifierChange, Time: Timing.CurTime + time, Type: type);
        consciousness.Modifiers[(modifierOwner, identifier)] = newModifier;

        UpdateConsciousnessModifiers(target, consciousness);
        Dirty(target, consciousness);

        return true;
    }

    [PublicAPI]
    public override bool ChangeConsciousnessModifier(EntityUid target,
        EntityUid modifierOwner,
        FixedPoint2 modifierChange,
        string identifier,
        TimeSpan? time = null,
        ConsciousnessComponent? consciousness = null)
    {
        if (!ConsciousnessQuery.Resolve(target, ref consciousness) ||
            !consciousness.Modifiers.TryGetValue((modifierOwner, identifier), out var oldModifier))
            return false;

        var newModifier =
            oldModifier with {Change = oldModifier.Change + modifierChange, Time = Timing.CurTime + time ?? oldModifier.Time};

        consciousness.Modifiers[(modifierOwner, identifier)] = newModifier;

        UpdateConsciousnessModifiers(target, consciousness);
        Dirty(target, consciousness);

        return true;
    }

    [PublicAPI]
    public override bool AddConsciousnessMultiplier(EntityUid target,
        EntityUid multiplierOwner,
        FixedPoint2 multiplier,
        string identifier = "Unspecified",
        ConsciousnessModType type = ConsciousnessModType.Generic,
        TimeSpan? time = null,
        ConsciousnessComponent? consciousness = null)
    {
        if (!ConsciousnessQuery.Resolve(target, ref consciousness))
            return false;

        if (!consciousness.Multipliers.TryAdd((multiplierOwner, identifier), new ConsciousnessMultiplier(multiplier, Timing.CurTime + time ?? time, type)))
            return false;

        UpdateConsciousnessMultipliers(target, consciousness);
        UpdateConsciousnessModifiers(target, consciousness);

        Dirty(target, consciousness);

        return true;
    }

    [PublicAPI]
    public override bool RemoveConsciousnessMultiplier(EntityUid target,
        EntityUid multiplierOwner,
        string identifier,
        ConsciousnessComponent? consciousness = null)
    {
        if (!ConsciousnessQuery.Resolve(target, ref consciousness))
            return false;

        if (!consciousness.Multipliers.Remove((multiplierOwner, identifier)))
            return false;

        UpdateConsciousnessMultipliers(target, consciousness);
        UpdateConsciousnessModifiers(target, consciousness);

        Dirty(target, consciousness);

        return true;
    }

    #endregion
}
