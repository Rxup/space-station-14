using System.Linq;
using Content.Shared.Backmen.Surgery.Pain.Components;
using Content.Shared.HealthExaminable;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Shared.Backmen.Surgery.Pain.Systems;

public abstract partial class PainSystem : EntitySystem
{
    [Dependency] protected IGameTiming Timing = default!;
    [Dependency] protected IConfigurationManager Cfg = default!;

    [Dependency] protected SharedAudioSystem IHaveNoMouthAndIMustScream = default!;
    [Dependency] private StatusEffectsSystem _statusEffects = default!;
    [Dependency] private SharedContainerSystem _container = default!;

    protected EntityQuery<NerveSystemComponent> NerveSystemQuery;
    protected EntityQuery<NerveOrganComponent> NerveQuery;
    protected EntityQuery<PainImmuneComponent> PainImmuneQuery;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NerveSystemComponent, AfterAutoHandleStateEvent>(OnNerveSystemAfterAutoHandleState);
        SubscribeLocalEvent<NerveSystemComponent, EntityTerminatingEvent>(OnNerveSystemTerminating);
        SubscribeLocalEvent<NerveOrganComponent, AfterAutoHandleStateEvent>(OnNerveAfterAutoHandleState);
        SubscribeLocalEvent<PainImmuneComponent, HealthBeingExaminedEvent>(OnPainImmuneHealthExamined);
        SubscribeLocalEvent<PainImmuneComponent, StatusEffectRelayedEvent<HealthBeingExaminedEvent>>(OnPainImmuneHealthExaminedRelayed);

        NerveSystemQuery = GetEntityQuery<NerveSystemComponent>();
        NerveQuery = GetEntityQuery<NerveOrganComponent>();
        PainImmuneQuery = GetEntityQuery<PainImmuneComponent>();
    }

    /// <summary>
    /// True if the entity has inherent <see cref="PainImmuneComponent"/> or a status effect that carries it.
    /// </summary>
    public bool IsPainImmune(EntityUid uid)
    {
        return PainImmuneQuery.HasComp(uid) || _statusEffects.HasEffectComp<PainImmuneComponent>(uid);
    }

    private void OnPainImmuneHealthExamined(Entity<PainImmuneComponent> ent, ref HealthBeingExaminedEvent args)
    {
        AddPainImmuneExamineText(args.Message, ent.Owner);
    }

    private void OnPainImmuneHealthExaminedRelayed(Entity<PainImmuneComponent> ent, ref StatusEffectRelayedEvent<HealthBeingExaminedEvent> args)
    {
        if (!_container.TryGetContainingContainer((ent.Owner, null, null), out var container))
            return;

        var target = container.Owner;
        if (PainImmuneQuery.HasComp(target))
            return;

        AddPainImmuneExamineText(args.Args.Message, target);
    }

    private void AddPainImmuneExamineText(FormattedMessage message, EntityUid target)
    {
        if (!message.IsEmpty)
            message.PushNewline();

        message.TryAddMarkup(Loc.GetString("pain-immune-health-examine", ("target", target)), out _);
    }

    private void OnNerveSystemTerminating(Entity<NerveSystemComponent> ent, ref EntityTerminatingEvent args)
    {
        // Clear every nerve that still points here — Nerves may be incomplete after amputations/rebuilds.
        var query = EntityQueryEnumerator<NerveOrganComponent>();
        while (query.MoveNext(out _, out var nerve))
        {
            if (nerve.ParentedNerveSystem != ent.Owner)
                continue;

            nerve.ParentedNerveSystem = EntityUid.Invalid;
        }

        ent.Comp.Nerves.Clear();
    }

    private void OnNerveSystemAfterAutoHandleState(Entity<NerveSystemComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        SanitizeNerveSystemDictionaries(ent.Comp);
    }

    private void OnNerveAfterAutoHandleState(Entity<NerveOrganComponent> ent, ref AfterAutoHandleStateEvent args)
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

    private void SanitizeNerveDictionaries(NerveOrganComponent component)
    {
        if (component.ParentedNerveSystem != EntityUid.Invalid && TerminatingOrDeleted(component.ParentedNerveSystem))
            component.ParentedNerveSystem = EntityUid.Invalid;

        foreach (var key in component.PainFeelingModifiers.Keys.ToArray())
        {
            if (TerminatingOrDeleted(key.Item1))
                component.PainFeelingModifiers.Remove(key);
        }
    }

    /// <summary>
    /// Rebuilds nerve links for a nerve system after organs are inserted or removed.
    /// </summary>
    public virtual void RefreshNerveSystem(EntityUid nerveSystemUid, EntityUid body)
    {
    }
}
