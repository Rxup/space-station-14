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
    [Dependency] protected readonly MobThresholdSystem MobThresholds = default!;

    protected EntityQuery<ConsciousnessComponent> ConsciousnessQuery;
    protected EntityQuery<MobStateComponent> MobStateQuery;

    public override void Initialize()
    {
        base.Initialize();

        InitNet();

        ConsciousnessQuery = GetEntityQuery<ConsciousnessComponent>();
        MobStateQuery = GetEntityQuery<MobStateComponent>();
    }

    protected void UpdateConsciousnessModifiers(Entity<ConsciousnessComponent?> uid)
    {
        if (!ConsciousnessQuery.Resolve(uid, ref uid.Comp))
            return;

        var totalDamage
            = uid.Comp.Modifiers.Aggregate(FixedPoint2.Zero,
                (current, modifier) => current + modifier.Value.Change * uid.Comp.Multiplier);

        var newConsciousness = uid.Comp.Cap + totalDamage;
        if (newConsciousness != uid.Comp.RawConsciousness)
        {
            var ev = new ConsciousnessChangedEvent(uid.Comp, newConsciousness, uid.Comp.RawConsciousness);
            RaiseLocalEvent(uid, ref ev);
        }

        uid.Comp.RawConsciousness = newConsciousness;

        CheckConscious(uid);
        Dirty(uid);
    }

    protected void UpdateConsciousnessMultipliers(Entity<ConsciousnessComponent?> uid)
    {
        if (!ConsciousnessQuery.Resolve(uid, ref uid.Comp))
            return;

        uid.Comp.Multiplier = uid.Comp.Multipliers.Count == 0
            ? FixedPoint2.New(1)
            : uid.Comp.Multipliers.Aggregate(FixedPoint2.Zero,
            (current, multiplier) => current + multiplier.Value.Change) / uid.Comp.Multipliers.Count;

        CheckConscious(uid);
        Dirty(uid);
    }

    /// <summary>
    /// Only used internally. Do not use this, instead use consciousness modifiers/multipliers!
    /// </summary>
    /// <param name="target">target entity</param>
    /// <param name="isConscious">should this entity be conscious</param>
    /// <param name="consciousness">consciousness component</param>
    protected void SetConscious(
        Entity<ConsciousnessComponent?> target,
        bool isConscious)
    {
        if (!Resolve(target, ref target.Comp))
            return;

        target.Comp.IsConscious = isConscious;
        Dirty(target);
    }

    protected void UpdateMobState(
        Entity<ConsciousnessComponent?, MobStateComponent?> target)
    {
        if (TerminatingOrDeleted(target)
            || !ConsciousnessQuery.Resolve(target, ref target.Comp1)
            || !MobStateQuery.Resolve(target, ref target.Comp2, false))
            return;

        var newMobState = MobState.Alive;
        if (TryGetNerveSystem(target, out var nerveSys))
        {
            var comp = nerveSys.Value.Comp;
            if (comp.Pain >= comp.SoftPainCap || comp.ForcePainCrit)
                newMobState = MobState.SoftCritical;
        }

        if (!target.Comp1.ForceConscious)
        {
            if (!target.Comp1.IsConscious)
                newMobState = MobState.Critical;

            if (target.Comp1.PassedOut)
                newMobState = MobState.Critical;

            if (target.Comp1.ForceUnconscious)
                newMobState = MobState.Critical;

            if (target.Comp1.Consciousness <= 0)
                newMobState = MobState.Dead;
        }

        if (target.Comp1.ForceDead)
            newMobState = MobState.Dead;

        MobStateSys.ChangeMobState(target, newMobState, target);
        MobThresholds.VerifyThresholds(target, mobState: target);
    }

    protected void CheckRequiredParts(
        Entity<ConsciousnessComponent> bodyId)
    {
        var alive = true;
        var conscious = true;

        foreach (var (/*identifier */_, (entity, forcesDeath, isLost)) in bodyId.Comp.RequiredConsciousnessParts)
        {
            if (entity == null || !isLost)
                continue;

            if (forcesDeath)
            {
                bodyId.Comp.ForceDead = true;
                Dirty(bodyId);

                alive = false;
                break;
            }

            conscious = false;
        }

        if (alive)
        {
            bodyId.Comp.ForceDead = false;
            bodyId.Comp.ForceUnconscious = !conscious;

            Dirty(bodyId);
        }

        CheckConscious(bodyId.AsNullable());
    }
}
