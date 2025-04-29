using System.Linq;
using Content.Shared.Backmen.CCVar;
using Content.Shared.Backmen.Surgery.Body.Events;
using Content.Shared.Backmen.Surgery.Consciousness.Systems;
using Content.Shared.Backmen.Surgery.Pain.Components;
using Content.Shared.Backmen.Surgery.Wounds;
using Content.Shared.Backmen.Surgery.Wounds.Systems;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Humanoid;
using Content.Shared.Mobs;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.GameStates;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared.Backmen.Surgery.Pain.Systems;

public abstract partial class PainSystem : EntitySystem
{
    [Dependency] protected readonly IGameTiming Timing = default!;
    [Dependency] protected readonly IRobustRandom Random = default!;

    [Dependency] protected readonly IConfigurationManager Cfg = default!;

    [Dependency] protected readonly SharedAudioSystem IHaveNoMouthAndIMustScream = default!;

    [Dependency] protected readonly ConsciousnessSystem Consciousness = default!;
    [Dependency] protected readonly WoundSystem Wound = default!;

    [Dependency] private readonly SharedBodySystem _body = default!;

    protected EntityQuery<NerveSystemComponent> NerveSystemQuery;
    protected EntityQuery<NerveComponent> NerveQuery;

    private float _universalPainMultiplier = 1f;
    private float _maxPainPerInflicter = 100f;

    protected bool PainEnabled = true;
    protected bool PainReflexesEnabled = true;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NerveComponent, ComponentHandleState>(OnComponentHandleState);
        SubscribeLocalEvent<NerveComponent, ComponentGetState>(OnComponentGet);

        SubscribeLocalEvent<NerveComponent, BodyPartAddedEvent>(OnBodyPartAdded, after: [typeof(ConsciousnessSystem)]);
        SubscribeLocalEvent<NerveComponent, BodyPartRemovedEvent>(OnBodyPartRemoved, after: [typeof(ConsciousnessSystem)]);

        SubscribeLocalEvent<PainInflicterComponent, WoundChangedEvent>(OnPainChanged);

        SubscribeLocalEvent<NerveSystemComponent, MobStateChangedEvent>(OnMobStateChanged);

        Subs.CVar(Cfg, CCVars.UniversalPainMultiplier, value => _universalPainMultiplier = value, true);
        Subs.CVar(Cfg, CCVars.PainInflicterCapacity, value => _maxPainPerInflicter = value, true);

        Subs.CVar(Cfg, CCVars.PainEnabled, value => PainEnabled = value, true);
        Subs.CVar(Cfg, CCVars.PainReflexesEnabled, value => PainReflexesEnabled = value, true);

        NerveSystemQuery = GetEntityQuery<NerveSystemComponent>();
        NerveQuery = GetEntityQuery<NerveComponent>();
    }

    protected const string PainAdrenalineIdentifier = "PainAdrenaline";

    protected const string PainModifierIdentifier = "WoundPain";
    private const string PainTraumaticModifierIdentifier = "TraumaticPain";

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

    private void OnBodyPartAdded(EntityUid uid, NerveComponent nerve, ref BodyPartAddedEvent args)
    {
        var bodyPart = Comp<BodyPartComponent>(uid);
        if (!bodyPart.Body.HasValue)
            return;

        if (!Consciousness.TryGetNerveSystem(bodyPart.Body.Value, out var brainUid) || TerminatingOrDeleted(brainUid.Value))
            return;

        UpdateNerveSystemNerves(brainUid.Value, bodyPart.Body.Value, Comp<NerveSystemComponent>(brainUid.Value));
    }

    private void OnBodyPartRemoved(EntityUid uid, NerveComponent nerve, ref BodyPartRemovedEvent args)
    {
        var bodyPart = Comp<BodyPartComponent>(uid);
        if (!bodyPart.Body.HasValue)
            return;

        if (!Consciousness.TryGetNerveSystem(bodyPart.Body.Value, out var brainUid) || TerminatingOrDeleted(brainUid.Value))
            return;

        foreach (var modifier in brainUid.Value.Comp.Modifiers
                     .Where(modifier => modifier.Key.Item1 == uid))
        {
            // Clean up pain of separated woundables
            brainUid.Value.Comp.Modifiers.Remove((modifier.Key.Item1, modifier.Key.Item2));
        }

        UpdateNerveSystemNerves(brainUid.Value, bodyPart.Body.Value, Comp<NerveSystemComponent>(brainUid.Value));
    }

    private void OnPainChanged(EntityUid uid, PainInflicterComponent component, WoundChangedEvent args)
    {
        if (!TryComp<BodyPartComponent>(args.Component.HoldingWoundable, out var bodyPart))
            return;

        if (bodyPart.Body == null)
            return;

        if (!Consciousness.TryGetNerveSystem(bodyPart.Body.Value, out var nerveSys))
            return;

        component.RawPain = FixedPoint2.Clamp(component.RawPain + args.Delta * _universalPainMultiplier, 0, _maxPainPerInflicter);

        var woundPain = FixedPoint2.Zero;
        var traumaticPain = FixedPoint2.Zero;

        foreach (var bp in _body.GetBodyChildren(bodyPart.Body))
        {
            foreach (var wound in Wound.GetWoundableWoundsWithComp<PainInflicterComponent>(bp.Id))
            {
                switch (wound.Comp2.PainType)
                {
                    case PainDamageTypes.TraumaticPain:
                        traumaticPain += wound.Comp2.Pain;
                        break;
                    default:
                        woundPain += wound.Comp2.Pain;
                        break;
                }
            }
        }

        if (!TryAddPainModifier(nerveSys.Value, args.Component.HoldingWoundable, PainModifierIdentifier, woundPain))
            TryChangePainModifier(nerveSys.Value, args.Component.HoldingWoundable, PainModifierIdentifier, woundPain);

        if (traumaticPain <= 0)
            return;

        if (!TryAddPainModifier(
                nerveSys.Value,
                args.Component.HoldingWoundable,
                PainTraumaticModifierIdentifier,
                traumaticPain,
                PainDamageTypes.TraumaticPain))
        {
            TryChangePainModifier(
                nerveSys.Value,
                args.Component.HoldingWoundable,
                PainTraumaticModifierIdentifier,
                traumaticPain);
        }
    }

    private void OnMobStateChanged(EntityUid uid, NerveSystemComponent nerveSys, MobStateChangedEvent args)
    {
        switch (args.NewMobState)
        {
            case MobState.Critical:
                var sex = Sex.Unsexed;
                if (TryComp<HumanoidAppearanceComponent>(args.Target, out var humanoid))
                    sex = humanoid.Sex;

                PlayPainSoundWithCleanup(args.Target, nerveSys, nerveSys.CritWhimpers[sex], AudioParams.Default.WithVolume(-12f));
                nerveSys.NextCritScream = Timing.CurTime + Random.Next(nerveSys.CritScreamsIntervalMin, nerveSys.CritScreamsIntervalMax);

                break;
            case MobState.Dead:
                CleanupSounds(nerveSys);

                break;
        }
    }

    private void UpdateNerveSystemNerves(EntityUid uid, EntityUid body, NerveSystemComponent component)
    {
        component.Nerves.Clear();
        foreach (var bodyPart in _body.GetBodyChildren(body))
        {
            if (!TryComp<NerveComponent>(bodyPart.Id, out var nerve))
                continue;

            component.Nerves.Add(bodyPart.Id, nerve);
            Dirty(uid, component);

            nerve.ParentedNerveSystem = uid;
            Dirty(bodyPart.Id, nerve); // ヾ(≧▽≦*)o
        }
    }
}
