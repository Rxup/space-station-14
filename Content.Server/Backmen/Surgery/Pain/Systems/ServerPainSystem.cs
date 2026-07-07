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
using Content.Shared.Body;
using Content.Shared.Body.Part;
using Content.Shared.Backmen.Body.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Humanoid;
using Content.Shared.Jittering;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Standing;
using JetBrains.Annotations;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.Random;

namespace Content.Server.Backmen.Surgery.Pain.Systems;

public sealed partial class ServerPainSystem : PainSystem
{
    [Dependency] private IRobustRandom _random = default!;

    [Dependency] private StandingStateSystem _standing = default!;
    [Dependency] private TraumaSystem _trauma = default!;

    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedJitteringSystem _jitter = default!;

    [Dependency] private BkmBodySharedSystem _body = default!;

    [Dependency] private MobStateSystem _mobState = default!;

    [Dependency] private ConsciousnessSystem _consciousness = default!;
    [Dependency] private WoundSystem _wound = default!;
    [Dependency] private EntityQuery<PainImmuneComponent> _painImmuneQuery = default!;

    private const string PainAdrenalineIdentifier = "PainAdrenaline";
    private const string PainPhantomPainIdentifier = "PhantomPain";

    private const string PainModifierIdentifier = "WoundPain";
    private const string PainTraumaticModifierIdentifier = "TraumaticPain";
    private const string PainStarvingModifierIdentifier = "Starving";

    private float _universalPainMultiplier = 1f;
    private float _maxPainPerInflicter = 100f;

    private bool _painEnabled = true;
    private bool _painReflexesEnabled = true;

    private EntityQuery<MobStateComponent> _mobStateQuery;
    private EntityQuery<HumanoidProfileComponent> _humanoidAppearanceQuery;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NerveOrganComponent, BodyPartAddedEvent>(OnBodyPartAdded, after: [typeof(ConsciousnessSystem)]);
        SubscribeLocalEvent<NerveOrganComponent, BodyPartRemovedEvent>(OnBodyPartRemoved, after: [typeof(ConsciousnessSystem)]);

        SubscribeLocalEvent<PainInflicterComponent, WoundChangedEvent>(OnPainChanged); // backmen: phantom-pain-fix

        SubscribeLocalEvent<NerveSystemComponent, MobStateChangedEvent>(OnMobStateChanged);

        Subs.CVar(Cfg, CCVars.UniversalPainMultiplier, value => _universalPainMultiplier = value, true);
        Subs.CVar(Cfg, CCVars.PainInflicterCapacity, value => _maxPainPerInflicter = value, true);

        Subs.CVar(Cfg, CCVars.PainEnabled, value => _painEnabled = value, true);
        Subs.CVar(Cfg, CCVars.PainReflexesEnabled, value => _painReflexesEnabled = value, true);

        _mobStateQuery = GetEntityQuery<MobStateComponent>();
        _humanoidAppearanceQuery = GetEntityQuery<HumanoidProfileComponent>();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_painEnabled)
            return;

        var q = EntityQueryEnumerator<NerveSystemComponent, OrganComponent, MetaDataComponent>();
        while (q.MoveNext(out var uid, out var nerveSys, out var organ, out var meta))
        {
            if (Paused(uid, meta))
                continue;

            var body = organ.Body;
            if (!body.HasValue)
                continue;

            if (!_mobStateQuery.TryComp(body.Value, out var mobState))
                continue;

            if (nerveSys.ForcePainCrit && nerveSys.ForcePainCritEnd < Timing.CurTime)
            {
                nerveSys.ForcePainCrit = false;
                _consciousness.CheckConscious(body.Value);
            }

            if (nerveSys.LastPainThreshold != nerveSys.Pain)
            {
                if (Timing.CurTime > nerveSys.UpdateTime)
                    nerveSys.LastPainThreshold = nerveSys.Pain;

                if (Timing.CurTime > nerveSys.ReactionUpdateTime)
                    UpdatePainThreshold(uid, body.Value, nerveSys);
            }

            var sex = Sex.Unsexed;
            if (_humanoidAppearanceQuery.TryComp(body.Value, out var humanoid))
                sex = humanoid.Sex;

            if (Timing.CurTime > nerveSys.NextPainScream)
            {
                switch (mobState.CurrentState)
                {
                    case MobState.Alive:
                        if (nerveSys.Pain > nerveSys.PainThresholds[PainReflexType.Agony])
                        {
                            CleanupPainSounds(uid, nerveSys);

                            var sound = nerveSys.PainedWhimpers[sex];
                            PlayPainSound(body.Value,
                                uid,
                                sound,
                                AudioParams.Default.WithVariation(0.04f).WithVolume(-8f),
                                nerveSys);

                            nerveSys.NextPainScream =
                                Timing.CurTime
                                + IHaveNoMouthAndIMustScream.GetAudioLength(IHaveNoMouthAndIMustScream.ResolveSound(sound))
                                + _random.Next(nerveSys.PainScreamsIntervalMin, nerveSys.PainScreamsIntervalMax);
                        }

                        break;
                    case MobState.Critical:
                        CleanupPainSounds(uid, nerveSys);

                        SoundSpecifier? sound1;
                        if (_trauma.HasBodyTrauma(body.Value, TraumaType.OrganDamage))
                        {
                            // If the person suffers organ damage, do funny gaggling sound :3
                            sound1 = nerveSys.OrganDamageWhimpersSounds[sex];
                        }
                        else
                        {
                            // Whimpering
                            sound1 = _random.Prob(0.21f) ? nerveSys.PainedWhimpers[sex] : nerveSys.CritWhimpers[sex];
                        }

                        PlayPainSound(body.Value,
                            uid,
                            sound1,
                            AudioParams.Default.WithVariation(0.04f).WithVolume(-12f),
                            nerveSys);

                        nerveSys.NextPainScream =
                            Timing.CurTime
                            + IHaveNoMouthAndIMustScream.GetAudioLength(IHaveNoMouthAndIMustScream.ResolveSound(sound1))
                            + _random.Next(nerveSys.PainScreamsIntervalMin, nerveSys.PainScreamsIntervalMax);

                        break;
                    case MobState.SoftCritical:
                        CleanupPainSounds(uid, nerveSys);

                        var sound2 = nerveSys.ExtremePainSounds[sex];
                        PlayPainSound(body.Value,
                            uid,
                            sound2,
                            AudioParams.Default.WithVariation(0.02f).WithVolume(12f),
                            nerveSys);

                        nerveSys.NextPainScream =
                            Timing.CurTime
                            + IHaveNoMouthAndIMustScream.GetAudioLength(IHaveNoMouthAndIMustScream.ResolveSound(sound2))
                            + _random.Next(nerveSys.PainScreamsIntervalMin, nerveSys.PainScreamsIntervalMax);

                        break;
                    case MobState.Dead:
                        break;
                }
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

    private void OnBodyPartAdded(Entity<NerveOrganComponent> nerve, ref BodyPartAddedEvent args)
    {
        var bodyPart = args.Part.Comp;
        if (!bodyPart.Body.HasValue)
            return;

        if (!_consciousness.TryGetNerveSystem(bodyPart.Body.Value, out var brainUid) || TerminatingOrDeleted(brainUid.Value))
            return;

        UpdateNerveSystemNerves(brainUid.Value, bodyPart.Body.Value, Comp<NerveSystemComponent>(brainUid.Value));
    }

    private void OnBodyPartRemoved(Entity<NerveOrganComponent> nerve, ref BodyPartRemovedEvent args)
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
                    PainPhantomPainIdentifier,
                    pain,
                    brainUid.Value,
                    TimeSpan.FromMinutes(1f),
                    PainType.TraumaticPain))
            {
                TryAddPainModifier(
                    brainUid.Value,
                    brainUid.Value.Comp.RootNerve,
                    PainPhantomPainIdentifier,
                    pain,
                    PainType.TraumaticPain,
                    brainUid.Value,
                    TimeSpan.FromMinutes(1f));
            }
        }

        UpdateNerveSystemNerves(brainUid.Value, bodyPart.Body.Value, Comp<NerveSystemComponent>(brainUid.Value));
    }

    private void OnPainChanged(EntityUid uid, PainInflicterComponent component, ref WoundChangedEvent args)
    {
        RefreshWoundablePain(args.Component.HoldingWoundable); // backmen: phantom-pain-fix
    }

    // start-backmen: phantom-pain-fix
    /// <summary>
    /// Recomputes wound-derived pain modifiers for a woundable from remaining wounds.
    /// Removes modifiers when no wounds contribute pain (fixes phantom pain after healing).
    /// </summary>
    [PublicAPI]
    public void RefreshWoundablePain(EntityUid woundable, WoundableComponent? component = null)
    {
        if (!TryComp(woundable, out component))
            return;

        EntityUid? bodyUid = null;
        if (TryComp<BodyPartComponent>(woundable, out var bodyPart))
            bodyUid = bodyPart.Body;
        else if (TryComp<OrganComponent>(woundable, out var organ))
            bodyUid = organ.Body;

        if (bodyUid == null || !_consciousness.TryGetNerveSystem(bodyUid.Value, out var nerveSys))
            return;

        var woundPain = FixedPoint2.Zero;
        var traumaticPain = FixedPoint2.Zero;

        foreach (var wound in _wound.GetWoundableWoundsWithComp<PainInflicterComponent>(woundable, component))
        {
            var inflicter = wound.Comp2;
            inflicter.RawPain = FixedPoint2.Clamp(wound.Comp1.WoundSeverityPoint, 0, _maxPainPerInflicter);
            Dirty(wound.Owner, inflicter);

            switch (inflicter.PainType)
            {
                case PainType.TraumaticPain:
                    traumaticPain += inflicter.Pain;
                    break;
                case PainType.Starving:
                    break;
                default:
                    woundPain += inflicter.Pain;
                    break;
            }
        }

        SetWoundablePainModifier(nerveSys.Value, woundable, PainModifierIdentifier, woundPain);
        SetWoundablePainModifier(nerveSys.Value, woundable, PainTraumaticModifierIdentifier, traumaticPain, PainType.TraumaticPain);
    }

    private void SetWoundablePainModifier(
        EntityUid nerveSysUid,
        EntityUid nerveUid,
        string identifier,
        FixedPoint2 pain,
        PainType painType = PainType.WoundPain)
    {
        if (!NerveSystemQuery.TryComp(nerveSysUid, out var nerveSys))
            return;

        if (pain <= FixedPoint2.Zero)
        {
            TryRemovePainModifier(nerveSysUid, nerveUid, identifier, nerveSys);
            return;
        }

        if (!TryAddPainModifier(nerveSysUid, nerveUid, identifier, pain, painType, nerveSys))
            TryChangePainModifier(nerveSysUid, nerveUid, identifier, pain, nerveSys, painType: painType);
    }
    // end-backmen: phantom-pain-fix

    private void OnMobStateChanged(EntityUid uid, NerveSystemComponent nerveSys, MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        CleanupPainSounds(uid, nerveSys);
    }

    #endregion

    #region Private Handling

    public override void RefreshNerveSystem(EntityUid nerveSystemUid, EntityUid body)
    {
        if (!NerveSystemQuery.TryComp(nerveSystemUid, out var nerveSys))
            return;

        UpdateNerveSystemNerves(nerveSystemUid, body, nerveSys);
    }

    private void UpdateNerveSystemNerves(EntityUid uid, EntityUid body, NerveSystemComponent component)
    {
        component.Nerves.Clear();
        foreach (var bodyPartId in _body.GetWoundableTargets(body))
        {
            if (!NerveQuery.TryComp(bodyPartId, out var nerve))
                continue;

            component.Nerves.Add(bodyPartId, nerve);

            nerve.ParentedNerveSystem = uid;
            DirtyField(bodyPartId, nerve, nameof(NerveOrganComponent.ParentedNerveSystem));
            UpdatePainFeels(bodyPartId, nerve);
        }
    }

    private void UpdatePainFeels(EntityUid nerveUid, NerveOrganComponent? nerveComp = null)
    {
        if (!NerveQuery.Resolve(nerveUid, ref nerveComp))
            return;

        EntityUid? bodyUid = null;
        if (TryComp<BodyPartComponent>(nerveUid, out var bodyPart))
            bodyUid = bodyPart.Body;
        else if (TryComp<OrganComponent>(nerveUid, out var organ))
            bodyUid = organ.Body;

        if (bodyUid == null)
            return;

        var painFeels = nerveComp.DefaultPainFeels;
        foreach (var modifier in nerveComp.PainFeelingModifiers.Values)
            painFeels += modifier.Change;

        if (nerveComp.PainFeels != painFeels)
        {
            nerveComp.PainFeels = painFeels;
            DirtyField(nerveUid, nerveComp, nameof(NerveOrganComponent.PainFeels));
        }

        var ev = new PainFeelsChangedEvent(nerveComp.ParentedNerveSystem, nerveUid, nerveComp.PainFeels);
        RaiseLocalEvent(nerveUid, ref ev);

        if (!TryComp<TargetingComponent>(bodyUid.Value, out var targeting))
            return;

        targeting.BodyStatus = _wound.GetWoundableStatesOnBodyPainFeels(bodyUid.Value);
        DirtyField(bodyUid.Value, targeting, nameof(TargetingComponent.BodyStatus));

        RaiseNetworkEvent(new TargetIntegrityChangeEvent(GetNetEntity(bodyUid.Value)), bodyUid.Value);
    }

    private void UpdateNerveSystemPain(EntityUid uid, NerveSystemComponent? nerveSys = null)
    {
        if (!NerveSystemQuery.Resolve(uid, ref nerveSys))
            return;

        if (!TryComp<OrganComponent>(uid, out var organ) || !organ.Body.HasValue)
            return;

        var totalPain = FixedPoint2.Zero;
        var woundPain = FixedPoint2.Zero;

        foreach (var modifier in nerveSys.Modifiers)
        {
            if (modifier.Value.PainType == PainType.WoundPain)
                woundPain += ApplyModifiersToPain(modifier.Key.Item1, modifier.Value.Change, nerveSys, modifier.Value.PainType);

            totalPain += ApplyModifiersToPain(modifier.Key.Item1, modifier.Value.Change, nerveSys, modifier.Value.PainType);
        }

        var newPain = FixedPoint2.Clamp(woundPain, 0, nerveSys.SoftPainCap) + totalPain - woundPain;

        // throw away the scream, so they instantly do not overlap pain sounds
        nerveSys.NextPainScream = Timing.CurTime + _random.Next(nerveSys.PainScreamsIntervalMin, nerveSys.PainScreamsIntervalMax);

        nerveSys.UpdateTime = Timing.CurTime + nerveSys.ThresholdUpdateTime;
        if (nerveSys.Pain != newPain)
            nerveSys.ReactionUpdateTime = Timing.CurTime + nerveSys.PainReactionTime;
        nerveSys.Pain = newPain;

        DirtyField(uid, nerveSys, nameof(NerveSystemComponent.Pain));

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

    private void UpdatePainThreshold(EntityUid uid, EntityUid body, NerveSystemComponent nerveSys)
    {
        if (_painImmuneQuery.HasComp(body))
            return;

        var painInput = nerveSys.Pain - nerveSys.LastPainThreshold;

        var nearestReflex = PainReflexType.None;
        foreach (var (reflex, threshold) in nerveSys.PainThresholds.OrderByDescending(kv => kv.Value))
        {
            if (painInput < threshold)
                continue;

            nearestReflex = reflex;
            break;
        }

        if (nearestReflex == PainReflexType.None)
            return;

        if (nerveSys.LastReflexType == nearestReflex && Timing.CurTime < nerveSys.UpdateTime)
            return;

        var ev1 = new PainThresholdTriggered((uid, nerveSys), nearestReflex, painInput);
        RaiseLocalEvent(body, ref ev1);

        if (ev1.Cancelled || _mobState.IsDead(body))
            return;

        var ev2 = new PainThresholdEffected((uid, nerveSys), nearestReflex, painInput);
        RaiseLocalEvent(body, ref ev2);

        nerveSys.LastReflexType = nearestReflex;

        ApplyPainReflexesEffects(body, (uid, nerveSys), nearestReflex);
    }

    private void ApplyPainReflexesEffects(EntityUid body, Entity<NerveSystemComponent> nerveSys, PainReflexType reaction)
    {
        if (!_painReflexesEnabled)
            return;

        var sex = Sex.Unsexed;
        if (TryComp<HumanoidProfileComponent>(body, out var humanoid))
            sex = humanoid.Sex;

        switch (reaction)
        {
            case PainReflexType.PainGrunt:
                CleanupPainSounds(nerveSys, nerveSys);
                PlayPainSound(body, nerveSys, nerveSys.Comp.PainGrunts[sex], AudioParams.Default.WithVariation(0.1f).WithVolume(-4f), nerveSys.Comp);

                break;
            case PainReflexType.PainFlinch:
                CleanupPainSounds(nerveSys, nerveSys);
                PlayPainSound(body, nerveSys, nerveSys.Comp.PainScreams[sex], AudioParams.Default.WithVariation(0.1f).WithVolume(4f), nerveSys: nerveSys.Comp);

                _popup.PopupPredicted(Loc.GetString("screams-and-flinches-pain", ("entity", body)), body, null, PopupType.MediumCaution);
                _jitter.DoJitter(body, TimeSpan.FromSeconds(0.9f), true, 24f, 1f);

                break;
            case PainReflexType.Agony:
                CleanupPainSounds(nerveSys);
                PlayPainSound(body, nerveSys, nerveSys.Comp.AgonyScreams[sex], AudioParams.Default.WithVariation(0.04f).WithVolume(6f), nerveSys: nerveSys);

                _popup.PopupPredicted(Loc.GetString("screams-in-agony", ("entity", body)), body, null, PopupType.MediumCaution);
                _jitter.DoJitter(body, nerveSys.Comp.PainShockCritDuration / 1.4f, true, 30f, 12f);

                break;
            case PainReflexType.PainShock:
                CleanupPainSounds(nerveSys);

                var screamSpecifier = nerveSys.Comp.PainShockScreams[sex];
                PlayPainSound(body, nerveSys, screamSpecifier, AudioParams.Default.WithVolume(8f), nerveSys);

                var sound = nerveSys.Comp.PainedWhimpers[sex];
                PlayPainSound(
                    nerveSys,
                    sound,
                    IHaveNoMouthAndIMustScream
                        .GetAudioLength(IHaveNoMouthAndIMustScream.ResolveSound(screamSpecifier)) + TimeSpan.FromSeconds(2),
                    AudioParams.Default.WithVolume(-8f),
                    nerveSys);

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

                ForcePainCrit(nerveSys, nerveSys.Comp.PainShockCritDuration, nerveSys);
                _jitter.DoJitter(body, nerveSys.Comp.PainShockCritDuration, true, 20f, 7f);

                // For the funnies :3
                _consciousness.ForceConscious(body, nerveSys.Comp.PainShockCritDuration * 0.99f);

                break;
            case PainReflexType.PainShockAndAgony:
                CleanupPainSounds(nerveSys);

                var agonySpecifier = nerveSys.Comp.ExtremePainSounds[sex]; // hell yeah
                PlayPainSound(body, nerveSys, agonySpecifier, AudioParams.Default.WithVolume(12f));

                var painWhimpers = nerveSys.Comp.PainedWhimpers[sex];
                PlayPainSound(
                    nerveSys,
                    painWhimpers,
                    IHaveNoMouthAndIMustScream
                        .GetAudioLength(IHaveNoMouthAndIMustScream.ResolveSound(agonySpecifier)) - TimeSpan.FromSeconds(2),
                    AudioParams.Default.WithVolume(-8f),
                    nerveSys);

                IHaveNoMouthAndIMustScream.PlayPvs(
                    nerveSys.Comp.PainRattles,
                    body,
                    AudioParams.Default.WithVolume(-8f));

                _popup.PopupPredicted(
                    _standing.IsDown(body)
                        ? Loc.GetString("screams-in-pain", ("entity", body))
                        : Loc.GetString("screams-and-falls-pain", ("entity", body)),
                    body,
                    null,
                    PopupType.MediumCaution);

                ForcePainCrit(nerveSys, nerveSys.Comp.PainShockCritDuration * 1.4f, nerveSys);
                _jitter.DoJitter(body, nerveSys.Comp.PainShockCritDuration * 1.4f, true, 20f, 7f);

                _consciousness.ForceConscious(body, nerveSys.Comp.PainShockCritDuration * 1.39f);

                break;
            case PainReflexType.None:
                break;
        }
    }

    #endregion

    #region Helpers

    [PublicAPI]
    public override void ForcePainCrit(EntityUid ent, TimeSpan time, NerveSystemComponent? nerveSys = null)
    {
        if (!Resolve(ent, ref nerveSys, false))
            return;

        nerveSys.ForcePainCritEnd = Timing.CurTime + time;
        nerveSys.ForcePainCrit = true;

        var organComp = Comp<OrganComponent>(ent);
        if (organComp.Body.HasValue)
            _consciousness.CheckConscious(organComp.Body.Value);
    }

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
        PainType? painType = null)
    {
        if (!NerveSystemQuery.Resolve(uid, ref nerveSys, false))
            return false;

        if (!nerveSys.Modifiers.TryGetValue((nerveUid, identifier), out var modifier))
            return false;

        var modifierToSet =
            modifier with {Change = change, Time = Timing.CurTime + time ?? modifier.Time, PainType = painType ?? modifier.PainType};
        nerveSys.Modifiers[(nerveUid, identifier)] = modifierToSet;

        var ev = new PainModifierChangedEvent(uid, nerveUid, modifier.Change);
        RaiseLocalEvent(uid, ref ev);

        UpdateNerveSystemPain(uid, nerveSys);

        return true;
    }

    [PublicAPI]
    public override bool TryAddPainModifier(
        EntityUid uid,
        EntityUid nerveUid,
        string identifier,
        FixedPoint2 change,
        PainType painType = PainType.WoundPain,
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

        return true;
    }

    [PublicAPI]
    public override bool TryAddPainFeelsModifier(
        EntityUid effectOwner,
        string identifier,
        EntityUid nerveUid,
        FixedPoint2 change,
        NerveOrganComponent? nerve = null,
        TimeSpan? time = null)
    {
        if (!NerveQuery.Resolve(nerveUid, ref nerve, false))
            return false;

        var modifier = new PainFeelingModifier(change, Timing.CurTime + time);
        if (!nerve.PainFeelingModifiers.TryAdd((effectOwner, identifier), modifier))
            return false;

        UpdatePainFeels(nerveUid);

        return true;
    }

    [PublicAPI]
    public override bool TryChangePainFeelsModifier(
        EntityUid effectOwner,
        string identifier,
        EntityUid nerveUid,
        FixedPoint2 change,
        NerveOrganComponent? nerve = null)
    {
        if (!NerveQuery.Resolve(nerveUid, ref nerve))
            return false;

        if (!nerve.PainFeelingModifiers.TryGetValue((effectOwner, identifier), out var modifier))
            return false;

        var modifierToSet =
            modifier with { Change = change};
        nerve.PainFeelingModifiers[(effectOwner, identifier)] = modifierToSet;

        UpdatePainFeels(nerveUid);

        return true;
    }

    [PublicAPI]
    public override bool TrySetPainFeelsModifier(
        EntityUid effectOwner,
        string identifier,
        EntityUid nerveUid,
        FixedPoint2 change,
        TimeSpan? time = null,
        NerveOrganComponent? nerve = null)
    {
        if (!NerveQuery.Resolve(nerveUid, ref nerve, false))
            return false;

        if (!nerve.PainFeelingModifiers.TryGetValue((effectOwner, identifier), out var modifier))
            return false;

        var modifierToSet = new PainFeelingModifier(Change: change, Time: Timing.CurTime + time ?? modifier.Time);
        nerve.PainFeelingModifiers[(effectOwner, identifier)] = modifierToSet;

        UpdatePainFeels(nerveUid);

        return true;
    }

    [PublicAPI]
    public override bool TrySetPainFeelsModifier(
        EntityUid effectOwner,
        string identifier,
        EntityUid nerveUid,
        TimeSpan time,
        NerveOrganComponent? nerve = null,
        FixedPoint2? change = null)
    {
        if (!NerveQuery.Resolve(nerveUid, ref nerve, false))
            return false;

        if (!nerve.PainFeelingModifiers.TryGetValue((effectOwner, identifier), out var modifier))
            return false;

        var modifierToSet = new PainFeelingModifier(Change: change ?? modifier.Change, Time: Timing.CurTime + time);
        nerve.PainFeelingModifiers[(effectOwner, identifier)] = modifierToSet;

        UpdatePainFeels(nerveUid);

        return true;
    }

    [PublicAPI]
    public override bool TryRemovePainFeelsModifier(
        EntityUid effectOwner,
        string identifier,
        EntityUid nerveUid,
        NerveOrganComponent? nerve = null)
    {
        if (!NerveQuery.Resolve(nerveUid, ref nerve, false))
            return false;

        nerve.PainFeelingModifiers.Remove((effectOwner, identifier));

        UpdatePainFeels(nerveUid);

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

        return true;
    }

    [PublicAPI]
    public override bool TryAddPainMultiplier(
        EntityUid uid,
        string identifier,
        FixedPoint2 change,
        PainType painType = PainType.WoundPain,
        NerveSystemComponent? nerveSys = null,
        TimeSpan? time = null)
    {
        if (!NerveSystemQuery.Resolve(uid, ref nerveSys, false))
            return false;

        var modifier = new PainMultiplier(change, identifier, painType, Timing.CurTime + time);
        if (!nerveSys.Multipliers.TryAdd(identifier, modifier))
            return false;

        UpdateNerveSystemPain(uid, nerveSys);

        return true;
    }

    [PublicAPI]
    public override bool TryChangePainMultiplier(
        EntityUid uid,
        string identifier,
        FixedPoint2 change,
        TimeSpan? time = null,
        PainType? painType = null,
        NerveSystemComponent? nerveSys = null)
    {
        if (!NerveSystemQuery.Resolve(uid, ref nerveSys, false))
            return false;

        if (!nerveSys.Multipliers.TryGetValue(identifier, out var multiplier))
            return false;

        var multiplierToSet =
            multiplier with {Change = change, Time = Timing.CurTime + time ?? multiplier.Time, PainType = painType ?? multiplier.PainType};
        nerveSys.Multipliers[identifier] = multiplierToSet;

        UpdateNerveSystemPain(uid, nerveSys);

        return true;
    }

    [PublicAPI]
    public override bool TryChangePainMultiplier(
        EntityUid uid,
        string identifier,
        TimeSpan time,
        FixedPoint2? change = null,
        PainType? painType = null,
        NerveSystemComponent? nerveSys = null)
    {
        if (!NerveSystemQuery.Resolve(uid, ref nerveSys, false))
            return false;

        if (!nerveSys.Multipliers.TryGetValue(identifier, out var multiplier))
            return false;

        var multiplierToSet =
            multiplier with {Change = change ?? multiplier.Change, Time = Timing.CurTime + time, PainType = painType ?? multiplier.PainType};
        nerveSys.Multipliers[identifier] = multiplierToSet;

        UpdateNerveSystemPain(uid, nerveSys);

        return true;
    }

    [PublicAPI]
    public override bool TryChangePainMultiplier(
        EntityUid uid,
        string identifier,
        PainType painType,
        FixedPoint2? change = null,
        TimeSpan? time = null,
        NerveSystemComponent? nerveSys = null)
    {
        if (!NerveSystemQuery.Resolve(uid, ref nerveSys, false))
            return false;

        if (!nerveSys.Multipliers.TryGetValue(identifier, out var multiplier))
            return false;

        var multiplierToSet =
            multiplier with {Change = change ?? multiplier.Change, Time = Timing.CurTime + time ?? multiplier.Time, PainType = painType};
        nerveSys.Multipliers[identifier] = multiplierToSet;

        UpdateNerveSystemPain(uid, nerveSys);

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

        return true;
    }

    #endregion
}
