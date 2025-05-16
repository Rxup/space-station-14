using System.Linq;
using Content.Shared.Backmen.CCVar;
using Content.Shared.Backmen.Surgery.Body.Events;
using Content.Shared.Backmen.Surgery.Consciousness;
using Content.Shared.Backmen.Surgery.Consciousness.Systems;
using Content.Shared.Backmen.Surgery.Pain;
using Content.Shared.Backmen.Surgery.Pain.Components;
using Content.Shared.Backmen.Surgery.Pain.Systems;
using Content.Shared.Backmen.Surgery.Traumas;
using Content.Shared.Backmen.Surgery.Traumas.Systems;
using Content.Shared.Backmen.Surgery.Wounds;
using Content.Shared.Backmen.Surgery.Wounds.Components;
using Content.Shared.Backmen.Surgery.Wounds.Systems;
using Content.Shared.Backmen.Targeting;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Humanoid;
using Content.Shared.Jittering;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Standing;
using Content.Shared.Stunnable;
using JetBrains.Annotations;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.Random;

namespace Content.Server.Backmen.Surgery.Pain.Systems;

public sealed class ServerPainSystem : PainSystem
{
    [Dependency] private readonly IRobustRandom _random = default!;

    [Dependency] private readonly StandingStateSystem _standing = default!;
    [Dependency] private readonly TraumaSystem _trauma = default!;

    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedJitteringSystem _jitter = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;

    [Dependency] private readonly SharedBodySystem _body = default!;

    [Dependency] private readonly MobStateSystem _mobState = default!;

    [Dependency] private readonly ConsciousnessSystem _consciousness = default!;
    [Dependency] private readonly WoundSystem _wound = default!;

    private const string PainAdrenalineIdentifier = "PainAdrenaline";
    private const string PainPhantomPainIdentfier = "PhantomPain";

    private const string PainModifierIdentifier = "WoundPain";
    private const string PainTraumaticModifierIdentifier = "TraumaticPain";

    private float _universalPainMultiplier = 1f;
    private float _maxPainPerInflicter = 100f;

    private bool _painEnabled = true;
    private bool _painReflexesEnabled = true;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NerveComponent, BodyPartAddedEvent>(OnBodyPartAdded, after: [typeof(ConsciousnessSystem)]);
        SubscribeLocalEvent<NerveComponent, BodyPartRemovedEvent>(OnBodyPartRemoved, after: [typeof(ConsciousnessSystem)]);

        SubscribeLocalEvent<PainInflicterComponent, WoundChangedEvent>(OnPainChanged);

        SubscribeLocalEvent<NerveSystemComponent, MobStateChangedEvent>(OnMobStateChanged);

        Subs.CVar(Cfg, CCVars.UniversalPainMultiplier, value => _universalPainMultiplier = value, true);
        Subs.CVar(Cfg, CCVars.PainInflicterCapacity, value => _maxPainPerInflicter = value, true);

        Subs.CVar(Cfg, CCVars.PainEnabled, value => _painEnabled = value, true);
        Subs.CVar(Cfg, CCVars.PainReflexesEnabled, value => _painReflexesEnabled = value, true);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_painEnabled)
            return;

        var q = EntityQueryEnumerator<NerveSystemComponent, MetaDataComponent>();
        while (q.MoveNext(out var uid, out var nerveSys, out var meta))
        {
            if (Paused(uid, meta))
                continue;

            if (!TryComp<OrganComponent>(uid, out var nerveSysOrgan))
                return;

            var body = nerveSysOrgan.Body;
            if (body == null)
                return;

            if (nerveSys.LastPainThreshold != nerveSys.Pain)
            {
                if (Timing.CurTime > nerveSys.UpdateTime)
                    nerveSys.LastPainThreshold = nerveSys.Pain;

                if (Timing.CurTime > nerveSys.ReactionUpdateTime)
                    UpdatePainThreshold(uid, nerveSys);
            }

            if (Timing.CurTime > nerveSys.NextCritScream && _mobState.IsCritical(body.Value))
            {
                var sex = Sex.Unsexed;
                if (TryComp<HumanoidAppearanceComponent>(body, out var humanoid))
                    sex = humanoid.Sex;

                CleanupPainSounds(uid, nerveSys);
                if (_trauma.HasBodyTrauma(body.Value, TraumaType.OrganDamage))
                {
                    // If the person suffers organ damage, do funny gaggling sound :3
                    PlayPainSound(body.Value,
                        uid,
                        nerveSys.OrganDamageWhimpersSounds[sex],
                        AudioParams.Default.WithVolume(-14f),
                        nerveSys);
                }
                else
                {
                    if (_random.Prob(0.34f))
                    {
                        // Play screaming with less chance
                        PlayPainSound(
                            body.Value,
                            uid,
                            nerveSys.PainShockScreams[sex],
                            AudioParams.Default.WithVolume(12f),
                            nerveSys);
                    }
                    else
                    {
                        // Whimpering
                        PlayPainSound(body.Value,
                            uid,                   // Pained or normal
                            _random.Prob(0.34f) ? nerveSys.PainShockWhimpers[sex] : nerveSys.CritWhimpers[sex],
                            AudioParams.Default.WithVolume(-15f),
                            nerveSys);
                    }
                }

                nerveSys.NextCritScream = Timing.CurTime + _random.Next(nerveSys.CritScreamsIntervalMin, nerveSys.CritScreamsIntervalMax);
            }

            foreach (var (key, value) in nerveSys.PainSoundsToPlay)
            {
                if (Timing.CurTime < value.Item2)
                    continue;

                PlayPainSound(body.Value, uid, key, value.Item1, nerveSys);
                nerveSys.PainSoundsToPlay.Remove(key);
            }

            foreach (var (key, value) in nerveSys.Modifiers)
            {
                if (Timing.CurTime > value.Time)
                    TryRemovePainModifier(uid, key.Item1, key.Item2, nerveSys);
            }

            foreach (var (key, value) in nerveSys.Multipliers)
            {
                if (Timing.CurTime > value.Time)
                    TryRemovePainMultiplier(uid, key, nerveSys);
            }

            foreach (var (ent, nerve) in nerveSys.Nerves)
            {
                foreach (var (key, value) in nerve.PainFeelingModifiers)
                {
                    if (Timing.CurTime > value.Time)
                        TryRemovePainFeelsModifier(key.Item1, key.Item2, ent, nerve);
                }
            }
        }
    }

    #region Event Handling

    private void OnBodyPartAdded(Entity<NerveComponent> nerve, ref BodyPartAddedEvent args)
    {
        var bodyPart = args.Part.Comp;
        if (!bodyPart.Body.HasValue)
            return;

        if (!_consciousness.TryGetNerveSystem(bodyPart.Body.Value, out var brainUid) || TerminatingOrDeleted(brainUid.Value))
            return;

        UpdateNerveSystemNerves(brainUid.Value, bodyPart.Body.Value, Comp<NerveSystemComponent>(brainUid.Value));
    }

    private void OnBodyPartRemoved(Entity<NerveComponent> nerve, ref BodyPartRemovedEvent args)
    {
        var bodyPart = args.Part.Comp;
        if (!bodyPart.Body.HasValue)
            return;

        if (!_consciousness.TryGetNerveSystem(bodyPart.Body.Value, out var brainUid) || TerminatingOrDeleted(brainUid.Value))
            return;

        foreach (var modifier in brainUid.Value.Comp.Modifiers
                     .Where(modifier => modifier.Key.Item1 == nerve.Owner))
        {
            // Clean up pain of separated woundables
            brainUid.Value.Comp.Modifiers.Remove((modifier.Key.Item1, modifier.Key.Item2));
        }

        if (nerve.Owner != brainUid.Value.Comp.RootNerve
            && !TerminatingOrDeleted(brainUid.Value.Comp.RootNerve))
        {
            var pain = Comp<WoundableComponent>(nerve.Owner).IntegrityCap / 3f;
            if (!TryChangePainModifier(
                    brainUid.Value,
                    brainUid.Value.Comp.RootNerve,
                    PainPhantomPainIdentfier,
                    pain,
                    brainUid.Value,
                    TimeSpan.FromMinutes(1f),
                    PainDamageTypes.TraumaticPain))
            {
                TryAddPainModifier(
                    brainUid.Value,
                    brainUid.Value.Comp.RootNerve,
                    PainPhantomPainIdentfier,
                    pain,
                    PainDamageTypes.TraumaticPain,
                    brainUid.Value,
                    TimeSpan.FromMinutes(1f));
            }
        }

        UpdateNerveSystemNerves(brainUid.Value, bodyPart.Body.Value, Comp<NerveSystemComponent>(brainUid.Value));
    }

    private void OnPainChanged(EntityUid uid, PainInflicterComponent component, ref WoundChangedEvent args)
    {
        if (!TryComp<BodyPartComponent>(args.Component.HoldingWoundable, out var bodyPart))
            return;

        if (bodyPart.Body == null)
            return;

        if (!_consciousness.TryGetNerveSystem(bodyPart.Body.Value, out var nerveSys))
            return;

        component.RawPain = FixedPoint2.Clamp(component.RawPain + args.Delta * _universalPainMultiplier, 0, _maxPainPerInflicter);

        var woundPain = FixedPoint2.Zero;
        var traumaticPain = FixedPoint2.Zero;

        foreach (var wound in
                 _wound.GetWoundableWoundsWithComp<PainInflicterComponent>(args.Component.HoldingWoundable))
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

                CleanupPainSounds(uid, nerveSys);
                PlayPainSound(args.Target, uid, nerveSys.CritWhimpers[sex], AudioParams.Default.WithVolume(-12f), nerveSys);
                nerveSys.NextCritScream = Timing.CurTime + _random.Next(nerveSys.CritScreamsIntervalMin, nerveSys.CritScreamsIntervalMax);

                break;
            case MobState.Dead:
                CleanupPainSounds(uid, nerveSys);

                break;
        }
    }

    #endregion

    #region Private Handling

    private void UpdateNerveSystemNerves(EntityUid uid, EntityUid body, NerveSystemComponent component)
    {
        component.Nerves.Clear();
        foreach (var bodyPart in _body.GetBodyChildren(body))
        {
            if (!NerveQuery.TryComp(bodyPart.Id, out var nerve))
                continue;

            component.Nerves.Add(bodyPart.Id, nerve);
            Dirty(uid, component);

            nerve.ParentedNerveSystem = uid;
            Dirty(bodyPart.Id, nerve); // ヾ(≧▽≦*)o
        }
    }

    private void UpdatePainFeels(EntityUid nerveUid, NerveComponent? nerveComp = null)
    {
        if (!NerveQuery.Resolve(nerveUid, ref nerveComp))
            return;

        var bodyPart = Comp<BodyPartComponent>(nerveUid);
        if (bodyPart.Body == null)
            return;

        var ev = new PainFeelsChangedEvent(nerveComp.ParentedNerveSystem, nerveUid, nerveComp.PainFeels);
        RaiseLocalEvent(nerveUid, ref ev);

        if (!TryComp<TargetingComponent>(bodyPart.Body.Value, out var targeting))
            return;

        targeting.BodyStatus = _wound.GetWoundableStatesOnBodyPainFeels(bodyPart.Body.Value);
        Dirty(bodyPart.Body.Value, targeting);

        RaiseNetworkEvent(new TargetIntegrityChangeEvent(GetNetEntity(bodyPart.Body.Value)), bodyPart.Body.Value);
    }

    private void UpdateNerveSystemPain(EntityUid uid, NerveSystemComponent? nerveSys = null)
    {
        if (!NerveSystemQuery.Resolve(uid, ref nerveSys))
            return;

        if (!TryComp<OrganComponent>(uid, out var organ) || organ.Body == null)
            return;

        var totalPain = FixedPoint2.Zero;
        var woundPain = FixedPoint2.Zero;

        foreach (var modifier in nerveSys.Modifiers)
        {
            if (modifier.Value.PainDamageType == PainDamageTypes.WoundPain)
                woundPain += ApplyModifiersToPain(modifier.Key.Item1, modifier.Value.Change, nerveSys, modifier.Value.PainDamageType);

            totalPain += ApplyModifiersToPain(modifier.Key.Item1, modifier.Value.Change, nerveSys, modifier.Value.PainDamageType);
        }

        var newPain = FixedPoint2.Clamp(woundPain, 0, nerveSys.SoftPainCap) + totalPain - woundPain;

        nerveSys.UpdateTime = Timing.CurTime + nerveSys.ThresholdUpdateTime;
        if (nerveSys.Pain != newPain)
            nerveSys.ReactionUpdateTime = Timing.CurTime + nerveSys.PainReactionTime;
        nerveSys.Pain = newPain;

        if (!_consciousness.SetConsciousnessModifier(
                organ.Body.Value,
                uid,
                -nerveSys.Pain,
                identifier: PainModifierIdentifier,
                type: ConsciousnessModType.Pain))
        {
            _consciousness.AddConsciousnessModifier(
                organ.Body.Value,
                uid,
                -nerveSys.Pain,
                identifier: PainModifierIdentifier,
                type: ConsciousnessModType.Pain);
        }
    }

    private void UpdatePainThreshold(EntityUid uid, NerveSystemComponent nerveSys)
    {
        var painInput = nerveSys.Pain - nerveSys.LastPainThreshold;

        var nearestReflex = PainThresholdTypes.None;
        foreach (var (reflex, threshold) in nerveSys.PainThresholds.OrderByDescending(kv => kv.Value))
        {
            if (painInput < threshold)
                continue;

            nearestReflex = reflex;
            break;
        }

        if (nearestReflex == PainThresholdTypes.None)
            return;

        if (nerveSys.LastThresholdType == nearestReflex && Timing.CurTime < nerveSys.UpdateTime)
            return;

        if (!TryComp<OrganComponent>(uid, out var organ) || !organ.Body.HasValue)
            return;

        var ev1 = new PainThresholdTriggered((uid, nerveSys), nearestReflex, painInput);
        RaiseLocalEvent(organ.Body.Value, ref ev1);

        if (ev1.Cancelled || _mobState.IsDead(organ.Body.Value))
            return;

        var ev2 = new PainThresholdEffected((uid, nerveSys), nearestReflex, painInput);
        RaiseLocalEvent(organ.Body.Value, ref ev2);

        nerveSys.LastThresholdType = nearestReflex;

        ApplyPainReflexesEffects(organ.Body.Value, (uid, nerveSys), nearestReflex);
    }

    private void ApplyPainReflexesEffects(EntityUid body, Entity<NerveSystemComponent> nerveSys, PainThresholdTypes reaction)
    {
        if (!_painReflexesEnabled)
            return;

        var sex = Sex.Unsexed;
        if (TryComp<HumanoidAppearanceComponent>(body, out var humanoid))
            sex = humanoid.Sex;

        switch (reaction)
        {
            case PainThresholdTypes.PainGrunt:
                CleanupPainSounds(nerveSys, nerveSys);
                PlayPainSound(body, nerveSys, nerveSys.Comp.PainGrunts[sex], nerveSys: nerveSys.Comp);

                break;
            case PainThresholdTypes.PainFlinch:
                CleanupPainSounds(nerveSys, nerveSys);
                PlayPainSound(body, nerveSys, nerveSys.Comp.PainScreams[sex], nerveSys: nerveSys.Comp);

                _popup.PopupPredicted(Loc.GetString("screams-and-flinches-pain", ("entity", body)), body, null, PopupType.MediumCaution);
                _jitter.DoJitter(body, TimeSpan.FromSeconds(0.9f), true, 24f, 1f);

                break;
            case PainThresholdTypes.Agony:
                CleanupPainSounds(nerveSys);
                PlayPainSound(body, nerveSys, nerveSys.Comp.AgonyScreams[sex], AudioParams.Default.WithVolume(12f), nerveSys);

                // We love violence, don't we?

                _popup.PopupPredicted(Loc.GetString("screams-in-agony", ("entity", body)), body, null, PopupType.MediumCaution);
                _jitter.DoJitter(body, nerveSys.Comp.PainShockStunTime / 1.4, true, 30f, 12f);

                break;
            case PainThresholdTypes.PainShock:
                CleanupPainSounds(nerveSys);

                var screamSpecifier = nerveSys.Comp.PainShockScreams[sex];
                PlayPainSound(body, nerveSys, screamSpecifier, AudioParams.Default.WithVolume(12f));

                var sound = nerveSys.Comp.PainShockWhimpers[sex];
                PlayPainSound(
                    nerveSys,
                    sound,
                    IHaveNoMouthAndIMustScream
                        .GetAudioLength(IHaveNoMouthAndIMustScream.ResolveSound(screamSpecifier)) + TimeSpan.FromSeconds(2),
                    AudioParams.Default.WithVolume(-15f),
                    nerveSys);

                IHaveNoMouthAndIMustScream.PlayPvs(
                    nerveSys.Comp.PainRattles,
                    body,
                    AudioParams.Default.WithVolume(-12f));

                TryAddPainMultiplier(
                    nerveSys,
                    PainAdrenalineIdentifier,
                    0.7f,
                    PainDamageTypes.WoundPain,
                    nerveSys,
                    nerveSys.Comp.PainShockAdrenalineTime);

                _popup.PopupPredicted(
                    _standing.IsDown(body)
                        ? Loc.GetString("screams-in-pain", ("entity", body))
                        : Loc.GetString("screams-and-falls-pain", ("entity", body)),
                    body,
                    null,
                    PopupType.MediumCaution);

                _stun.TryParalyze(body, nerveSys.Comp.PainShockStunTime, true);
                _jitter.DoJitter(body, nerveSys.Comp.PainShockStunTime, true, 20f, 7f);

                // For the funnies :3
                _consciousness.ForceConscious(body, nerveSys.Comp.PainShockStunTime);

                break;
            case PainThresholdTypes.PainShockAndAgony:
                CleanupPainSounds(nerveSys);

                var agonySpecifier = nerveSys.Comp.AgonyScreams[sex];
                PlayPainSound(body, nerveSys, agonySpecifier, AudioParams.Default.WithVolume(12f));

                var painWhimpers = nerveSys.Comp.PainShockWhimpers[sex];
                PlayPainSound(
                    nerveSys,
                    painWhimpers,
                    IHaveNoMouthAndIMustScream
                        .GetAudioLength(IHaveNoMouthAndIMustScream.ResolveSound(agonySpecifier)) - TimeSpan.FromSeconds(2),
                    AudioParams.Default.WithVolume(-15f));

                IHaveNoMouthAndIMustScream.PlayPvs(
                    nerveSys.Comp.PainRattles,
                    body,
                    AudioParams.Default.WithVolume(-12f));

                _popup.PopupPredicted(
                    _standing.IsDown(body)
                        ? Loc.GetString("screams-in-pain", ("entity", body))
                        : Loc.GetString("screams-and-falls-pain", ("entity", body)),
                    body,
                    null,
                    PopupType.MediumCaution);

                _stun.TryParalyze(body, nerveSys.Comp.PainShockStunTime * 1.4, true);
                _jitter.DoJitter(body, nerveSys.Comp.PainShockStunTime * 1.4, true, 20f, 7f);

                _consciousness.ForceConscious(body, nerveSys.Comp.PainShockStunTime * 1.4);

                break;
            case PainThresholdTypes.None:
                break;
        }
    }

    #endregion

    #region Helpers

    [PublicAPI]
    public override void CleanupPainSounds(EntityUid ent, NerveSystemComponent? nerveSys = null)
    {
        var killAllSounds = new KillAllPainSoundsEvent(GetNetEntity(ent));
        RaiseNetworkEvent(killAllSounds);
    }

    [PublicAPI]
    public override Entity<AudioComponent>? PlayPainSound(EntityUid body, SoundSpecifier specifier, AudioParams? audioParams = null)
    {
        var playPainSoundEv = new PlayPainSoundEvent(specifier, GetNetEntity(body), audioParams);
        RaiseNetworkEvent(playPainSoundEv);

        return null; // Don't return the sound
    }

    [PublicAPI]
    public override Entity<AudioComponent>? PlayPainSound(
        EntityUid body,
        EntityUid nerveSysEnt,
        SoundSpecifier specifier,
        AudioParams? audioParams = null,
        NerveSystemComponent? nerveSys = null)
    {
        if (!NerveSystemQuery.Resolve(nerveSysEnt, ref nerveSys))
            return null;

        var beforePainSound = new BeforePainSoundPlayed((nerveSysEnt, nerveSys), specifier);
        RaiseLocalEvent(body, ref beforePainSound);

        if (beforePainSound.Cancelled)
            return null;

        var playPainSoundEv = new PlayLoggedPainSoundEvent(
            GetNetEntity(nerveSysEnt),
            specifier,
            TryGetNetEntity(body, out var ne) ? ne.Value : null,
            audioParams);
        RaiseNetworkEvent(playPainSoundEv);

        return null; // Don't return the sound
    }

    [PublicAPI]
    public override bool TryChangePainModifier(
        EntityUid uid,
        EntityUid nerveUid,
        string identifier,
        FixedPoint2 change,
        NerveSystemComponent? nerveSys = null,
        TimeSpan? time = null,
        PainDamageTypes? painType = null)
    {
        if (!NerveSystemQuery.Resolve(uid, ref nerveSys, false))
            return false;

        if (!nerveSys.Modifiers.TryGetValue((nerveUid, identifier), out var modifier))
            return false;

        var modifierToSet =
            modifier with {Change = change, Time = Timing.CurTime + time ?? modifier.Time, PainDamageType = painType ?? modifier.PainDamageType};
        nerveSys.Modifiers[(nerveUid, identifier)] = modifierToSet;

        var ev = new PainModifierChangedEvent(uid, nerveUid, modifier.Change);
        RaiseLocalEvent(uid, ref ev);

        UpdateNerveSystemPain(uid, nerveSys);
        Dirty(uid, nerveSys);

        return true;
    }

    [PublicAPI]
    public override bool TryAddPainModifier(
        EntityUid uid,
        EntityUid nerveUid,
        string identifier,
        FixedPoint2 change,
        PainDamageTypes painType = PainDamageTypes.WoundPain,
        NerveSystemComponent? nerveSys = null,
        TimeSpan? time = null)
    {
        if (!NerveSystemQuery.Resolve(uid, ref nerveSys, false))
            return false;

        var modifier = new PainModifier(change, MetaData(nerveUid).EntityPrototype!.ID, painType, Timing.CurTime + time);
        if (!nerveSys.Modifiers.TryAdd((nerveUid, identifier), modifier))
            return false;

        var ev = new PainModifierAddedEvent(uid, nerveUid, change);
        RaiseLocalEvent(uid, ref ev);

        UpdateNerveSystemPain(uid, nerveSys);
        Dirty(uid, nerveSys);

        return true;
    }

    [PublicAPI]
    public override bool TryAddPainFeelsModifier(
        EntityUid effectOwner,
        string identifier,
        EntityUid nerveUid,
        FixedPoint2 change,
        NerveComponent? nerve = null,
        TimeSpan? time = null)
    {
        if (!NerveQuery.Resolve(nerveUid, ref nerve, false))
            return false;

        var modifier = new PainFeelingModifier(change, Timing.CurTime + time);
        if (!nerve.PainFeelingModifiers.TryAdd((effectOwner, identifier), modifier))
            return false;

        UpdatePainFeels(nerveUid);

        Dirty(nerveUid, nerve);
        return true;
    }

    [PublicAPI]
    public override bool TryChangePainFeelsModifier(
        EntityUid effectOwner,
        string identifier,
        EntityUid nerveUid,
        FixedPoint2 change,
        NerveComponent? nerve = null)
    {
        if (!NerveQuery.Resolve(nerveUid, ref nerve))
            return false;

        if (!nerve.PainFeelingModifiers.TryGetValue((effectOwner, identifier), out var modifier))
            return false;

        var modifierToSet =
            modifier with { Change = change};
        nerve.PainFeelingModifiers[(effectOwner, identifier)] = modifierToSet;

        UpdatePainFeels(nerveUid);

        Dirty(nerveUid, nerve);
        return true;
    }

    [PublicAPI]
    public override bool TrySetPainFeelsModifier(
        EntityUid effectOwner,
        string identifier,
        EntityUid nerveUid,
        FixedPoint2 change,
        TimeSpan? time = null,
        NerveComponent? nerve = null)
    {
        if (!NerveQuery.Resolve(nerveUid, ref nerve, false))
            return false;

        if (!nerve.PainFeelingModifiers.TryGetValue((effectOwner, identifier), out var modifier))
            return false;

        var modifierToSet = new PainFeelingModifier(Change: change, Time: Timing.CurTime + time ?? modifier.Time);
        nerve.PainFeelingModifiers[(effectOwner, identifier)] = modifierToSet;

        UpdatePainFeels(nerveUid);

        Dirty(nerveUid, nerve);
        return true;
    }

    [PublicAPI]
    public override bool TrySetPainFeelsModifier(
        EntityUid effectOwner,
        string identifier,
        EntityUid nerveUid,
        TimeSpan time,
        NerveComponent? nerve = null,
        FixedPoint2? change = null)
    {
        if (!NerveQuery.Resolve(nerveUid, ref nerve, false))
            return false;

        if (!nerve.PainFeelingModifiers.TryGetValue((effectOwner, identifier), out var modifier))
            return false;

        var modifierToSet = new PainFeelingModifier(Change: change ?? modifier.Change, Time: Timing.CurTime + time);
        nerve.PainFeelingModifiers[(effectOwner, identifier)] = modifierToSet;

        UpdatePainFeels(nerveUid);

        Dirty(nerveUid, nerve);
        return true;
    }

    [PublicAPI]
    public override bool TryRemovePainFeelsModifier(
        EntityUid effectOwner,
        string identifier,
        EntityUid nerveUid,
        NerveComponent? nerve = null)
    {
        if (!NerveQuery.Resolve(nerveUid, ref nerve, false))
            return false;

        nerve.PainFeelingModifiers.Remove((effectOwner, identifier));

        UpdatePainFeels(nerveUid);
        Dirty(nerveUid, nerve);

        return true;
    }

    [PublicAPI]
    public override bool TryRemovePainModifier(
        EntityUid uid,
        EntityUid nerveUid,
        string identifier,
        NerveSystemComponent? nerveSys = null)
    {
        if (!NerveSystemQuery.Resolve(uid, ref nerveSys, false))
            return false;

        if (!nerveSys.Modifiers.Remove((nerveUid, identifier)))
            return false;

        var ev = new PainModifierRemovedEvent(uid, nerveUid, nerveSys.Pain);
        RaiseLocalEvent(uid, ref ev);

        UpdateNerveSystemPain(uid, nerveSys);
        Dirty(uid, nerveSys);

        return true;
    }

    [PublicAPI]
    public override bool TryAddPainMultiplier(
        EntityUid uid,
        string identifier,
        FixedPoint2 change,
        PainDamageTypes painType = PainDamageTypes.WoundPain,
        NerveSystemComponent? nerveSys = null,
        TimeSpan? time = null)
    {
        if (!NerveSystemQuery.Resolve(uid, ref nerveSys, false))
            return false;

        var modifier = new PainMultiplier(change, identifier, painType, Timing.CurTime + time);
        if (!nerveSys.Multipliers.TryAdd(identifier, modifier))
            return false;

        UpdateNerveSystemPain(uid, nerveSys);

        Dirty(uid, nerveSys);
        return true;
    }

    [PublicAPI]
    public override bool TryChangePainMultiplier(
        EntityUid uid,
        string identifier,
        FixedPoint2 change,
        TimeSpan? time = null,
        PainDamageTypes? painType = null,
        NerveSystemComponent? nerveSys = null)
    {
        if (!NerveSystemQuery.Resolve(uid, ref nerveSys, false))
            return false;

        if (!nerveSys.Multipliers.TryGetValue(identifier, out var multiplier))
            return false;

        var multiplierToSet =
            multiplier with {Change = change, Time = Timing.CurTime + time ?? multiplier.Time, PainDamageType = painType ?? multiplier.PainDamageType};
        nerveSys.Multipliers[identifier] = multiplierToSet;

        UpdateNerveSystemPain(uid, nerveSys);
        Dirty(uid, nerveSys);

        return true;
    }

    [PublicAPI]
    public override bool TryChangePainMultiplier(
        EntityUid uid,
        string identifier,
        TimeSpan time,
        FixedPoint2? change = null,
        PainDamageTypes? painType = null,
        NerveSystemComponent? nerveSys = null)
    {
        if (!NerveSystemQuery.Resolve(uid, ref nerveSys, false))
            return false;

        if (!nerveSys.Multipliers.TryGetValue(identifier, out var multiplier))
            return false;

        var multiplierToSet =
            multiplier with {Change = change ?? multiplier.Change, Time = Timing.CurTime + time, PainDamageType = painType ?? multiplier.PainDamageType};
        nerveSys.Multipliers[identifier] = multiplierToSet;

        UpdateNerveSystemPain(uid, nerveSys);
        Dirty(uid, nerveSys);

        return true;
    }

    [PublicAPI]
    public override bool TryChangePainMultiplier(
        EntityUid uid,
        string identifier,
        PainDamageTypes painType,
        FixedPoint2? change = null,
        TimeSpan? time = null,
        NerveSystemComponent? nerveSys = null)
    {
        if (!NerveSystemQuery.Resolve(uid, ref nerveSys, false))
            return false;

        if (!nerveSys.Multipliers.TryGetValue(identifier, out var multiplier))
            return false;

        var multiplierToSet =
            multiplier with {Change = change ?? multiplier.Change, Time = Timing.CurTime + time ?? multiplier.Time, PainDamageType = painType};
        nerveSys.Multipliers[identifier] = multiplierToSet;

        UpdateNerveSystemPain(uid, nerveSys);
        Dirty(uid, nerveSys);

        return true;
    }

    [PublicAPI]
    public override bool TryRemovePainMultiplier(
        EntityUid uid,
        string identifier,
        NerveSystemComponent? nerveSys = null)
    {
        if (!NerveSystemQuery.Resolve(uid, ref nerveSys, false))
            return false;

        if (!nerveSys.Multipliers.Remove(identifier))
            return false;

        UpdateNerveSystemPain(uid, nerveSys);
        Dirty(uid, nerveSys);

        return true;
    }

    #endregion
}
