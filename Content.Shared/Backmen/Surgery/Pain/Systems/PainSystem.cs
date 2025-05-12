using Content.Shared.Backmen.Surgery.Pain.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.GameStates;
using Robust.Shared.Timing;

namespace Content.Shared.Backmen.Surgery.Pain.Systems;

public abstract partial class PainSystem : EntitySystem
{
    [Dependency] protected readonly IGameTiming Timing = default!;
    [Dependency] protected readonly IConfigurationManager Cfg = default!;

    [Dependency] protected readonly SharedAudioSystem IHaveNoMouthAndIMustScream = default!;

    protected EntityQuery<NerveSystemComponent> NerveSystemQuery;
    protected EntityQuery<NerveComponent> NerveQuery;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NerveComponent, ComponentHandleState>(OnComponentHandleState);
        SubscribeLocalEvent<NerveComponent, ComponentGetState>(OnComponentGet);

        NerveSystemQuery = GetEntityQuery<NerveSystemComponent>();
        NerveQuery = GetEntityQuery<NerveComponent>();
    }

    private void OnComponentHandleState(EntityUid uid, NerveComponent component, ref ComponentHandleState args)
    {
        if (args.Current is not NerveComponentState state)
            return;

        component.ParentedNerveSystem = TryGetEntity(state.ParentedNerveSystem, out var e) ? e.Value : EntityUid.Invalid;
        component.PainMultiplier = state.PainMultiplier;

        component.PainFeelingModifiers.Clear();
        foreach (var ((modEntity, id), modifier) in state.PainFeelingModifiers)
        {
            component.PainFeelingModifiers.Add((TryGetEntity(modEntity, out var e1) ? e1.Value : EntityUid.Invalid, id), modifier);
        }
    }

    private void OnComponentGet(EntityUid uid, NerveComponent comp, ref ComponentGetState args)
    {
        var state = new NerveComponentState();

        state.ParentedNerveSystem = TryGetNetEntity(comp.ParentedNerveSystem, out var ne) ? ne.Value : NetEntity.Invalid;
        state.PainMultiplier = comp.PainMultiplier;

        foreach (var ((modEntity, id), modifier) in comp.PainFeelingModifiers)
        {
            state.PainFeelingModifiers.Add((TryGetNetEntity(modEntity, out var ne1) ? ne1.Value : NetEntity.Invalid, id), modifier);
        }

        args.State = state;
    }
}
