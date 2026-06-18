using System.Linq;
using Content.Shared.Backmen.Surgery.Consciousness.Components;
using Content.Shared.Backmen.Surgery.Pain.Components;
using Content.Shared.Backmen.Surgery.Pain.Systems;
using Content.Shared.Backmen.Surgery.Wounds.Systems;
using Content.Shared.Body.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Zombies;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared.Backmen.Surgery.Consciousness.Systems;

public abstract partial class ConsciousnessSystem : EntitySystem
{
    [Dependency] protected IGameTiming Timing = default!;

    [Dependency] protected IRobustRandom Random = default!;
    [Dependency] protected IPrototypeManager Proto = default!;

    [Dependency] protected SharedBodySystem Body = default!;

    [Dependency] protected PainSystem Pain = default!;
    [Dependency] protected WoundSystem Wound = default!;

    [Dependency] protected MobStateSystem MobStateSys = default!;
    [Dependency] protected MobThresholdSystem MobThresholds = default!;

    [Dependency] protected EntityQuery<ConsciousnessComponent> ConsciousnessQuery = default!;
    [Dependency] protected EntityQuery<MobStateComponent> MobStateQuery = default!;
    [Dependency] protected EntityQuery<PainImmuneComponent> PainImmuneQuery = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ConsciousnessComponent, MobStateChangedEvent>(OnRelayState);
        SubscribeLocalEvent<ConsciousnessComponent, AfterAutoHandleStateEvent>(OnAfterAutoHandleState);
    }

    private void OnAfterAutoHandleState(Entity<ConsciousnessComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        SanitizeConsciousnessDictionaries(ent.Comp);
    }

    private void SanitizeConsciousnessDictionaries(ConsciousnessComponent component)
    {
        if (component.NerveSystem != null && TerminatingOrDeleted(component.NerveSystem.Value.Owner))
            component.NerveSystem = null;

        foreach (var key in component.Modifiers.Keys.ToArray())
        {
            if (TerminatingOrDeleted(key.Item1))
                component.Modifiers.Remove(key);
        }

        foreach (var key in component.Multipliers.Keys.ToArray())
        {
            if (TerminatingOrDeleted(key.Item1))
                component.Multipliers.Remove(key);
        }

        foreach (var (id, (entity, _, _)) in component.RequiredConsciousnessParts.ToArray())
        {
            if (entity != null && TerminatingOrDeleted(entity.Value))
                component.RequiredConsciousnessParts.Remove(id);
        }
    }

    protected virtual void OnMobStateChanged(Entity<ConsciousnessComponent> consciousness,
        ref MobStateChangedEvent args)
    {
        // do nothing
    }

    private void OnRelayState(Entity<ConsciousnessComponent> ent, ref MobStateChangedEvent args)
    {
        if (TryGetNerveSystem(ent.AsNullable(), out var nerveSys))
        {
            RaiseLocalEvent(nerveSys.Value, args);
        }

        OnMobStateChanged(ent, ref args);
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
        DirtyField(uid, uid.Comp, nameof(ConsciousnessComponent.RawConsciousness));
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
        DirtyField(uid, uid.Comp, nameof(ConsciousnessComponent.Multiplier));
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
        DirtyField(target, target.Comp, nameof(ConsciousnessComponent.IsConscious));
    }

    protected void UpdateMobState(
        Entity<ConsciousnessComponent?, MobStateComponent?> target)
    {
        if (TerminatingOrDeleted(target)
            || !ConsciousnessQuery.Resolve(target, ref target.Comp1)
            || !MobStateQuery.Resolve(target, ref target.Comp2, false))
            return;

        // Zombies are revived corpses; consciousness must not re-apply death/unconscious states.
        if (HasComp<ZombieComponent>(target.Owner))
            return;

        var newMobState = MobState.Alive;
        if (!PainImmuneQuery.HasComp(target) && TryGetNerveSystem(target, out var nerveSys))
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
                DirtyField(bodyId, bodyId.Comp, nameof(ConsciousnessComponent.ForceDead));

                alive = false;
                break;
            }

            conscious = false;
        }

        if (alive)
        {
            bodyId.Comp.ForceDead = false;
            bodyId.Comp.ForceUnconscious = !conscious;

            DirtyFields(bodyId, bodyId.Comp, null,
                nameof(ConsciousnessComponent.ForceDead),
                nameof(ConsciousnessComponent.ForceUnconscious));
        }

        CheckConscious(bodyId.AsNullable());
    }
}
