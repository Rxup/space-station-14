using System.Linq;
using Content.Shared.Backmen.Surgery.Pain.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.GameStates;
using Robust.Shared.Timing;

namespace Content.Shared.Backmen.Surgery.Pain.Systems;

public abstract partial class PainSystem : EntitySystem
{
    [Dependency] protected IGameTiming Timing = default!;
    [Dependency] protected IConfigurationManager Cfg = default!;

    [Dependency] protected SharedAudioSystem IHaveNoMouthAndIMustScream = default!;

    protected EntityQuery<NerveSystemComponent> NerveSystemQuery;
    protected EntityQuery<NerveComponent> NerveQuery;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NerveSystemComponent, AfterAutoHandleStateEvent>(OnNerveSystemAfterAutoHandleState);
        SubscribeLocalEvent<NerveComponent, AfterAutoHandleStateEvent>(OnNerveAfterAutoHandleState);

        NerveSystemQuery = GetEntityQuery<NerveSystemComponent>();
        NerveQuery = GetEntityQuery<NerveComponent>();
    }

    private void OnNerveSystemAfterAutoHandleState(Entity<NerveSystemComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        SanitizeNerveSystemDictionaries(ent.Comp);
    }

    private void OnNerveAfterAutoHandleState(Entity<NerveComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        SanitizeNerveDictionaries(ent.Comp);
    }

    private void SanitizeNerveSystemDictionaries(NerveSystemComponent component)
    {
        foreach (var key in component.Modifiers.Keys.ToArray())
        {
            if (TerminatingOrDeleted(key.Item1))
                component.Modifiers.Remove(key);
        }
    }

    private void SanitizeNerveDictionaries(NerveComponent component)
    {
        foreach (var key in component.PainFeelingModifiers.Keys.ToArray())
        {
            if (TerminatingOrDeleted(key.Item1))
                component.PainFeelingModifiers.Remove(key);
        }
    }
}
