using System.Linq;
using Content.Shared.Backmen.Surgery.Consciousness;
using Content.Shared.Backmen.Surgery.Pain;
using Content.Shared.Backmen.Surgery.Pain.Components;
using Content.Shared.Backmen.Surgery.Pain.Systems;
using Content.Shared.Backmen.Surgery.Traumas;
using Content.Shared.Backmen.Surgery.Traumas.Systems;
using Content.Shared.Backmen.Targeting;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Part;
using Content.Shared.FixedPoint;
using Content.Shared.Humanoid;
using Content.Shared.Jittering;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Standing;
using Content.Shared.Stunnable;
using JetBrains.Annotations;
using Robust.Shared.Audio;
using Robust.Shared.Random;

namespace Content.Server.Backmen.Surgery.Pain.Systems;

public sealed class ServerPainSystem : PainSystem
{
    [Dependency] private readonly StandingStateSystem _standing = default!;
    [Dependency] private readonly TraumaSystem _trauma = default!;

    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedJitteringSystem _jitter = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;

    [Dependency] private readonly MobStateSystem _mobState = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!PainEnabled)
            return;

        var q = EntityQueryEnumerator<NerveSystemComponent, MetaDataComponent>();
        while (q.MoveNext(out var uid, out var nerveSys, out var meta))
        {
            if (Paused(uid, meta))
                continue;

            if (!TryComp<OrganComponent>(uid, out var nerveSysOrgan))
                return;

            if (nerveSys.LastPainThreshold != nerveSys.Pain)
            {
                if (Timing.CurTime > nerveSys.UpdateTime)
                    nerveSys.LastPainThreshold = nerveSys.Pain;

                if (Timing.CurTime > nerveSys.ReactionUpdateTime)
                    UpdatePainThreshold(uid, nerveSys);
            }

            if (Timing.CurTime > nerveSys.NextCritScream)
            {
                var body = nerveSysOrgan.Body;
                if (body != null && _mobState.IsCritical(body.Value))
                {
                    var sex = Sex.Unsexed;
                    if (TryComp<HumanoidAppearanceComponent>(body, out var humanoid))
                        sex = humanoid.Sex;

                    CleanupSounds(nerveSys);
                    if (_trauma.HasBodyTrauma(body.Value, TraumaType.OrganDamage))
                    {
                        // If the person suffers organ damage, do funny gaggling sound :3
                        PlayPainSound(body.Value,
                            nerveSys,
                            nerveSys.OrganDamageWhimpersSounds[sex],
                            AudioParams.Default.WithVolume(-12f));
                    }
                    else
                    {
                        if (Random.Prob(0.34f))
                        {
                            // Play screaming with less chance
                            PlayPainSound(body.Value, nerveSys, nerveSys.PainShockScreams[sex], AudioParams.Default.WithVolume(12f));
                        }
                        else
                        {
                            // Whimpering
                            PlayPainSound(body.Value,
                                nerveSys,                    // Pained or normal
                                Random.Prob(0.34f) ? nerveSys.PainShockWhimpers[sex] : nerveSys.CritWhimpers[sex],
                                AudioParams.Default.WithVolume(-12f));
                        }
                    }

                    nerveSys.NextCritScream = Timing.CurTime + Random.Next(nerveSys.CritScreamsIntervalMin, nerveSys.CritScreamsIntervalMax);
                }
            }

            foreach (var (key, value) in nerveSys.PainSoundsToPlay)
            {
                if (Timing.CurTime < value.Item3)
                    continue;

                PlayPainSound(key, nerveSys, value.Item1, value.Item2);
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

    #region Private Handling

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

        targeting.BodyStatus = Wound.GetWoundableStatesOnBodyPainFeels(bodyPart.Body.Value);
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

        if (!Consciousness.SetConsciousnessModifier(
                organ.Body.Value,
                uid,
                -nerveSys.Pain,
                identifier: PainModifierIdentifier,
                type: ConsciousnessModType.Pain))
        {
            Consciousness.AddConsciousnessModifier(
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
        if (!PainReflexesEnabled)
            return;

        var sex = Sex.Unsexed;
        if (TryComp<HumanoidAppearanceComponent>(body, out var humanoid))
            sex = humanoid.Sex;

        switch (reaction)
        {
            case PainThresholdTypes.PainFlinch:
                CleanupSounds(nerveSys.Comp);
                PlayPainSound(body, nerveSys.Comp, nerveSys.Comp.PainScreams[sex]);

                _popup.PopupPredicted(Loc.GetString("screams-and-flinches-pain", ("entity", body)), body, null, PopupType.MediumCaution);
                _jitter.DoJitter(body, TimeSpan.FromSeconds(0.9f), true, 24f, 1f);

                break;
            case PainThresholdTypes.Agony:
                CleanupSounds(nerveSys);
                PlayPainSound(body, nerveSys, nerveSys.Comp.AgonyScreams[sex], AudioParams.Default.WithVolume(12f));

                // We love violence, don't we?

                _popup.PopupPredicted(Loc.GetString("screams-in-agony", ("entity", body)), body, null, PopupType.MediumCaution);
                _jitter.DoJitter(body, nerveSys.Comp.PainShockStunTime / 1.4, true, 30f, 12f);

                // They aren't put into Pain Sounds, because they aren't supposed to stop after an entity finishes jerking around in pain
                IHaveNoMouthAndIMustScream.PlayPvs(
                    nerveSys.Comp.PainRattles,
                    body,
                    AudioParams.Default.WithVolume(-12f));

                break;
            case PainThresholdTypes.PainShock:
                CleanupSounds(nerveSys);

                var screamSpecifier = nerveSys.Comp.PainShockScreams[sex];
                var scream = PlayPainSound(body, nerveSys, screamSpecifier, AudioParams.Default.WithVolume(12f));
                if (scream.HasValue)
                {
                    var sound = nerveSys.Comp.PainShockWhimpers[sex];
                    PlayPainSound(body,
                        nerveSys,
                        sound,
                        IHaveNoMouthAndIMustScream.GetAudioLength(IHaveNoMouthAndIMustScream.ResolveSound(screamSpecifier)) + TimeSpan.FromSeconds(2),
                        AudioParams.Default.WithVolume(-12f));
                }

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
                Consciousness.ForceConscious(body, nerveSys.Comp.PainShockStunTime);

                break;
            case PainThresholdTypes.PainShockAndAgony:
                CleanupSounds(nerveSys);

                var agonySpecifier = nerveSys.Comp.AgonyScreams[sex];
                var agony = PlayPainSound(body, nerveSys, agonySpecifier, AudioParams.Default.WithVolume(12f));
                if (agony.HasValue)
                {
                    var sound = nerveSys.Comp.PainShockWhimpers[sex];
                    PlayPainSound(body,
                        nerveSys,
                        sound,
                        IHaveNoMouthAndIMustScream.GetAudioLength(IHaveNoMouthAndIMustScream.ResolveSound(agonySpecifier)) - TimeSpan.FromSeconds(2),
                        AudioParams.Default.WithVolume(-12f));
                }

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

                Consciousness.ForceConscious(body, nerveSys.Comp.PainShockStunTime * 1.4);

                break;
            case PainThresholdTypes.None:
                break;
        }
    }

    #endregion

    #region Helpers

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
