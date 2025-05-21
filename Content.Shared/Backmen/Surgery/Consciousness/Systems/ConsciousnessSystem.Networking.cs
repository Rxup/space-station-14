using Content.Shared.Backmen.Surgery.Consciousness.Components;
using Robust.Shared.GameStates;

namespace Content.Shared.Backmen.Surgery.Consciousness.Systems;

public partial class ConsciousnessSystem
{
    private void InitNet()
    {
        SubscribeLocalEvent<ConsciousnessComponent, ComponentGetState>(OnComponentGet);
        SubscribeLocalEvent<ConsciousnessComponent, ComponentHandleState>(OnComponentHandleState);
    }

    private void OnComponentHandleState(EntityUid uid, ConsciousnessComponent component, ref ComponentHandleState args)
    {
        if (args.Current is not ConsciousnessComponentState state)
            return;

        component.Threshold = state.Threshold;
        component.RawConsciousness = state.RawConsciousness;
        component.Multiplier = state.Multiplier;
        component.Cap = state.Cap;
        component.ForceDead = state.ForceDead;
        component.ForceUnconscious = state.ForceUnconscious;
        component.IsConscious = state.IsConscious;
        component.Modifiers.Clear();
        component.Multipliers.Clear();
        component.RequiredConsciousnessParts.Clear();

        foreach (var ((modEntity, modType), modifier) in state.Modifiers)
        {
            var ent = TryGetEntity(modEntity, out var nem) ? nem.Value : EntityUid.Invalid;
            if (ent.Valid && !TerminatingOrDeleted(ent))
                component.Modifiers.TryAdd((ent, modType), modifier);
        }

        foreach (var ((multiplierEntity, multiplierType), modifier) in state.Multipliers)
        {
            var ent = TryGetEntity(multiplierEntity, out var nem) ? nem.Value : EntityUid.Invalid;
            if (ent.Valid && !TerminatingOrDeleted(ent))
                component.Multipliers.TryAdd((ent, multiplierType), modifier);
        }

        foreach (var (id, (entity, causesDeath, isLost)) in state.RequiredConsciousnessParts)
        {
            var ent = TryGetEntity(entity, out var ne) ? ne.Value : EntityUid.Invalid;
            if (ent.Valid && !TerminatingOrDeleted(ent))
                component.RequiredConsciousnessParts.TryAdd(id, (ent, causesDeath, isLost));
        }
    }

    private void OnComponentGet(EntityUid uid, ConsciousnessComponent comp, ref ComponentGetState args)
    {
        var state = new ConsciousnessComponentState
        {
            Threshold = comp.Threshold,
            RawConsciousness = comp.RawConsciousness,
            Multiplier = comp.Multiplier,
            Cap = comp.Cap,
            ForceDead = comp.ForceDead,
            ForceUnconscious = comp.ForceUnconscious,
            IsConscious = comp.IsConscious,
        };

        foreach (var ((modEntity, modType), modifier) in comp.Modifiers)
        {
            state.Modifiers.Add((TryGetNetEntity(modEntity, out var e) ? e.Value : NetEntity.Invalid, modType), modifier);
        }

        foreach (var ((multiplierEntity, multiplierType), modifier) in comp.Multipliers)
        {
            state.Multipliers.Add((TryGetNetEntity(multiplierEntity, out var e) ? e.Value : NetEntity.Invalid, multiplierType), modifier);
        }

        foreach (var (id, (entity, causesDeath, isLost)) in comp.RequiredConsciousnessParts)
        {
            state.RequiredConsciousnessParts.Add(id, (TryGetNetEntity(entity, out var e) ? e.Value : NetEntity.Invalid, causesDeath, isLost));
        }

        args.State = state;
    }
}
