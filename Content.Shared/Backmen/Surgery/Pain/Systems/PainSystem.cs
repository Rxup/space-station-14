using System.Linq;
using Content.Shared.Backmen.Surgery.Pain.Components;
using Content.Shared.HealthExaminable;
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
        SubscribeLocalEvent<NerveSystemComponent, EntityTerminatingEvent>(OnNerveSystemTerminating);
        SubscribeLocalEvent<NerveComponent, AfterAutoHandleStateEvent>(OnNerveAfterAutoHandleState);
        SubscribeLocalEvent<PainImmuneComponent, HealthBeingExaminedEvent>(OnPainImmuneHealthExamined);

        NerveSystemQuery = GetEntityQuery<NerveSystemComponent>();
        NerveQuery = GetEntityQuery<NerveComponent>();
    }

    private void OnPainImmuneHealthExamined(Entity<PainImmuneComponent> ent, ref HealthBeingExaminedEvent args)
    {
        if (!args.Message.IsEmpty)
            args.Message.PushNewline();

        args.Message.TryAddMarkup(Loc.GetString("pain-immune-health-examine", ("target", ent.Owner)), out _);
    }

    private void OnNerveSystemTerminating(Entity<NerveSystemComponent> ent, ref EntityTerminatingEvent args)
    {
        foreach (var (nerveUid, _) in ent.Comp.Nerves.ToArray())
        {
            if (!NerveQuery.TryComp(nerveUid, out var nerve))
                continue;

            nerve.ParentedNerveSystem = EntityUid.Invalid;
        }
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
        if (component.ParentedNerveSystem != EntityUid.Invalid && TerminatingOrDeleted(component.ParentedNerveSystem))
            component.ParentedNerveSystem = EntityUid.Invalid;

        foreach (var key in component.PainFeelingModifiers.Keys.ToArray())
        {
            if (TerminatingOrDeleted(key.Item1))
                component.PainFeelingModifiers.Remove(key);
        }
    }
}
