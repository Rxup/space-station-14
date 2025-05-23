using System.Linq;
using Content.Shared.Backmen.Surgery.Consciousness.Components;
using Content.Shared.Backmen.Surgery.Pain.Systems;
using Content.Shared.Backmen.Surgery.Wounds.Systems;
using Content.Shared.Body.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared.Backmen.Surgery.Consciousness.Systems;

public abstract partial class ConsciousnessSystem : EntitySystem
{
    [Dependency] protected readonly IGameTiming Timing = default!;

    [Dependency] protected readonly IRobustRandom Random = default!;
    [Dependency] protected readonly IPrototypeManager Proto = default!;

    [Dependency] protected readonly SharedBodySystem Body = default!;

    [Dependency] protected readonly PainSystem Pain = default!;
    [Dependency] protected readonly WoundSystem Wound = default!;

    [Dependency] protected readonly MobStateSystem MobStateSys = default!;

    protected EntityQuery<ConsciousnessComponent> ConsciousnessQuery;
    protected EntityQuery<MobStateComponent> MobStateQuery;

    public override void Initialize()
    {
        base.Initialize();

        InitNet();

        ConsciousnessQuery = GetEntityQuery<ConsciousnessComponent>();
        MobStateQuery = GetEntityQuery<MobStateComponent>();
    }

    protected void UpdateConsciousnessModifiers(EntityUid uid, ConsciousnessComponent? consciousness)
    {
        if (!ConsciousnessQuery.Resolve(uid, ref consciousness))
            return;

        var totalDamage
            = consciousness.Modifiers.Aggregate(FixedPoint2.Zero,
                (current, modifier) => current + modifier.Value.Change * consciousness.Multiplier);

        var newConsciousness = consciousness.Cap + totalDamage;
        if (newConsciousness != consciousness.RawConsciousness)
        {
            var ev = new ConsciousnessChangedEvent(consciousness, newConsciousness, consciousness.RawConsciousness);
            RaiseLocalEvent(uid, ref ev);
        }

        consciousness.RawConsciousness = newConsciousness;

        CheckConscious(uid, consciousness);
        Dirty(uid, consciousness);
    }

    protected void UpdateConsciousnessMultipliers(EntityUid uid, ConsciousnessComponent? consciousness)
    {
        if (!ConsciousnessQuery.Resolve(uid, ref consciousness))
            return;

        consciousness.Multiplier = consciousness.Multipliers.Count == 0
            ? FixedPoint2.New(1)
            : consciousness.Multipliers.Aggregate(FixedPoint2.Zero,
            (current, multiplier) => current + multiplier.Value.Change) / consciousness.Multipliers.Count;

        CheckConscious(uid, consciousness);
        Dirty(uid, consciousness);
    }

    /// <summary>
    /// Only used internally. Do not use this, instead use consciousness modifiers/multipliers!
    /// </summary>
    /// <param name="target">target entity</param>
    /// <param name="isConscious">should this entity be conscious</param>
    /// <param name="consciousness">consciousness component</param>
    protected void SetConscious(
        EntityUid target,
        bool isConscious,
        ConsciousnessComponent? consciousness = null)
    {
        if (!Resolve(target, ref consciousness))
            return;

        consciousness.IsConscious = isConscious;
        Dirty(target, consciousness);
    }

    protected void UpdateMobState(
        EntityUid target,
        ConsciousnessComponent? consciousness = null,
        MobStateComponent? mobState = null)
    {
        if (TerminatingOrDeleted(target)
            || !ConsciousnessQuery.Resolve(target, ref consciousness)
            || !MobStateQuery.Resolve(target, ref mobState, false))
            return;

        var newMobState = MobState.Alive;
        if (TryGetNerveSystem(target, out var nerveSys))
        {
            var comp = nerveSys.Value.Comp;
            if (comp.Pain >= comp.SoftPainCap || comp.ForcePainCrit)
                newMobState = MobState.SoftCritical;
        }

        if (!consciousness.ForceConscious)
        {
            if (!consciousness.IsConscious)
                newMobState = MobState.Critical;

            if (consciousness.PassedOut)
                newMobState = MobState.Critical;

            if (consciousness.ForceUnconscious)
                newMobState = MobState.Critical;

            if (consciousness.Consciousness <= 0)
                newMobState = MobState.Dead;
        }

        if (consciousness.ForceDead)
            newMobState = MobState.Dead;

        MobStateSys.ChangeMobState(target, newMobState, mobState);
    }

    protected void CheckRequiredParts(
        EntityUid bodyId,
        ConsciousnessComponent consciousness)
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
}
