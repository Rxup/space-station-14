using System.Linq;
using Content.Shared.Backmen.Surgery.Pain.Components;
using Content.Shared.Backmen.Surgery.Wounds;
using Content.Shared.Body.Organ;
using Content.Shared.FixedPoint;
using Content.Shared.Humanoid;
using Content.Shared.Popups;

namespace Content.Shared.Backmen.Surgery.Pain.Systems;

public partial class PainSystem
{
    #region Data

    private readonly Dictionary<WoundSeverity, FixedPoint2> _painMultipliers = new()
    {
        { WoundSeverity.Healed, 0.34 },
        { WoundSeverity.Minor, 0.34 },
        { WoundSeverity.Moderate, 0.47 },
        { WoundSeverity.Severe, 0.54 },
        { WoundSeverity.Critical, 0.62 },
        { WoundSeverity.Loss, 0.62 }, // already painful enough
    };

    #endregion

    #region Public API

    /// <summary>
    /// Change pain for specific nerve, if there's any. Adds MORE PAIN to it basically.
    /// </summary>
    /// <param name="uid">Uid of the nerveSystem component owner.</param>
    /// <param name="nerveUid">Nerve uid.</param>
    /// <param name="change">How many pain to set.</param>
    /// <param name="nerveSys">NerveSystemComponent.</param>
    /// <returns>Returns true, if PAIN QUOTA WAS COLLECTED.</returns>
    public bool TryChangePainModifier(EntityUid uid, EntityUid nerveUid, FixedPoint2 change, NerveSystemComponent? nerveSys = null)
    {
        if (!Resolve(uid, ref nerveSys, false) || _net.IsClient)
            return false;

        if (!nerveSys.Modifiers.TryGetValue(nerveUid, out var modifier))
            return false;

        var modifierToSet =
            modifier with {Change = change};
        nerveSys.Modifiers[nerveUid] = modifierToSet;

        var ev = new PainModifierChangedEvent(uid, nerveUid, modifier.Change);
        RaiseLocalEvent(uid, ref ev);

        UpdateNerveSystemPain(uid, nerveSys);
        Dirty(uid, nerveSys);

        return true;
    }

    /// <summary>
    /// Gets copy of a pain modifier.
    /// </summary>
    /// <param name="uid">Uid of the nerveSystem component owner.</param>
    /// <param name="nerveUid">Nerve uid, used to seek for modifier..</param>
    /// <param name="modifier">Modifier copy acquired.</param>
    /// <param name="nerveSys">NerveSystemComponent.</param>
    /// <returns>Returns true, if modifier was acquired.</returns>
    public bool TryGetPainModifier(EntityUid uid, EntityUid nerveUid, out PainModifier? modifier, NerveSystemComponent? nerveSys = null)
    {
        modifier = null;
        if (_net.IsClient)
            return false;

        if (!Resolve(uid, ref nerveSys, false))
            return false;

        if (!nerveSys.Modifiers.TryGetValue(nerveUid, out var data))
            return false;

        modifier = data;
        return true;
    }

    /// <summary>
    /// Adds pain to needed nerveSystem, uses modifiers.
    /// </summary>
    /// <param name="uid">Uid of the nerveSystem owner.</param>
    /// <param name="nerveUid">Uid of the nerve, to which damage was applied.</param>
    /// <param name="change">Number of pain to add.</param>
    /// <param name="nerveSys">NerveSystem component.</param>
    /// <returns>Returns true, if PAIN WAS APPLIED.</returns>
    public bool TryAddPainModifier(EntityUid uid, EntityUid nerveUid, FixedPoint2 change, NerveSystemComponent? nerveSys = null)
    {
        if (!Resolve(uid, ref nerveSys, false) || _net.IsClient)
            return false;

        var modifier = new PainModifier(change, MetaData(nerveUid).EntityPrototype!.ID);
        if (!nerveSys.Modifiers.TryAdd(nerveUid, modifier))
            return false;

        var ev = new PainModifierAddedEvent(uid, nerveUid, change);
        RaiseLocalEvent(uid, ref ev);

        UpdateNerveSystemPain(uid, nerveSys);
        Dirty(uid, nerveSys);

        return true;
    }

    /// <summary>
    /// Removes pain modifier.
    /// </summary>
    /// <param name="uid">NerveSystemComponent owner.</param>
    /// <param name="nerveUid">Nerve Uid, to which pain is applied.</param>
    /// <param name="nerveSys">NerveSystemComponent.</param>
    /// <returns>Returns true, if pain modifier is removed.</returns>
    public bool TryRemovePainModifier(EntityUid uid, EntityUid nerveUid, NerveSystemComponent? nerveSys = null)
    {
        if (!Resolve(uid, ref nerveSys, false) || _net.IsClient)
            return false;

        if (!nerveSys.Modifiers.Remove(nerveUid))
            return false;

        var ev = new PainModifierRemovedEvent(uid, nerveUid, nerveSys.Pain);
        RaiseLocalEvent(uid, ref ev);

        UpdateNerveSystemPain(uid, nerveSys);
        Dirty(uid, nerveSys);

        return true;
    }

    /// <summary>
    /// Adds pain multiplier to nerveSystem.
    /// </summary>
    /// <param name="uid">NerveSystem owner's uid.</param>
    /// <param name="identifier">ID for the multiplier.</param>
    /// <param name="change">Number to multiply.</param>
    /// <param name="nerveSys">NerveSystemComponent.</param>
    /// <returns>Returns true, if multiplier was applied.</returns>
    public bool TryAddPainMultiplier(EntityUid uid, string identifier, FixedPoint2 change, NerveSystemComponent? nerveSys = null)
    {
        if (!Resolve(uid, ref nerveSys, false) || _net.IsClient)
            return false;

        var modifier = new PainMultiplier(change, identifier);
        if (!nerveSys.Multipliers.TryAdd(identifier, modifier))
            return false;

        UpdateNerveSystemPain(uid, nerveSys);

        Dirty(uid, nerveSys);
        return true;
    }

    /// <summary>
    /// Adds pain multiplier to nerveSystem.
    /// </summary>
    /// <param name="uid">NerveSystem owner's uid.</param>
    /// <param name="identifier">ID for the multiplier.</param>
    /// <param name="change">Number to multiply.</param>
    /// <param name="nerveSys">NerveSystemComponent.</param>
    /// <returns>Returns true, if multiplier was applied.</returns>
    public bool TryChangePainMultiplier(EntityUid uid, string identifier, FixedPoint2 change, NerveSystemComponent? nerveSys = null)
    {
        if (!Resolve(uid, ref nerveSys, false) || _net.IsClient)
            return false;

        if (!nerveSys.Multipliers.TryGetValue(identifier, out var multiplier))
            return false;

        var multiplierToSet =
            multiplier with {Change = change};
        nerveSys.Multipliers[identifier] = multiplierToSet;

        UpdateNerveSystemPain(uid, nerveSys);
        Dirty(uid, nerveSys);

        return true;
    }

    /// <summary>
    /// Removes pain multiplier.
    /// </summary>
    /// <param name="uid">NerveSystem owner's uid.</param>
    /// <param name="identifier">ID to seek for the multiplier, what must be removed.</param>
    /// <param name="nerveSys">NerveSystemComponent.</param>
    /// <returns>Returns true, if multiplier was removed.</returns>
    public bool TryRemovePainMultiplier(EntityUid uid, string identifier, NerveSystemComponent? nerveSys = null)
    {
        if (!Resolve(uid, ref nerveSys, false) || _net.IsClient)
            return false;

        if (!nerveSys.Multipliers.Remove(identifier))
            return false;

        UpdateNerveSystemPain(uid, nerveSys);

        Dirty(uid, nerveSys);
        return true;
    }

    public EntityUid? GetNerveSystem(EntityUid? body)
    {
        foreach (var (id, _) in _body.GetBodyOrgans(body))
        {
            if (HasComp<NerveSystemComponent>(id))
                return id;
        }

        return EntityUid.Invalid;
    }

    #endregion

    #region Private API

    private void UpdateDamage(float frameTime)
    {
        var query = EntityQueryEnumerator<NerveSystemComponent>();
        while (query.MoveNext(out _, out var nerveSys))
        {
            if (nerveSys.LastPainThreshold == nerveSys.Pain)
                continue;

            if (_timing.CurTime < nerveSys.UpdateTime)
                continue;

            nerveSys.LastPainThreshold = nerveSys.Pain;
        }
    }

    private void UpdateNerveSystemPain(EntityUid uid, NerveSystemComponent? nerveSys = null)
    {
        if (!Resolve(uid, ref nerveSys, false) || !_net.IsServer)
            return;

        nerveSys.Pain =
            FixedPoint2.Clamp(
                nerveSys.Modifiers.Aggregate((FixedPoint2) 0,
                    (current, modifier) =>
                    current + ApplyModifiersToPain(modifier.Key, modifier.Value.Change, nerveSys)),
                0,
                nerveSys.PainCap);

        UpdatePainThreshold(uid, nerveSys);
    }

    private void ApplyPainReflexesEffects(EntityUid body, NerveSystemComponent nerveSys, PainThresholdTypes reaction)
    {
        if (!_net.IsServer)
            return;

        var sex = Sex.Unsexed;
        if (TryComp<HumanoidAppearanceComponent>(body, out var humanoid))
            sex = humanoid.Sex;

        switch (reaction)
        {
            case PainThresholdTypes.PainFlinch:
                foreach (var (sound, _) in nerveSys.PlayedPainSounds.Where(sound => !TerminatingOrDeleted(sound.Key)))
                {
                    _IHaveNoMouthAndIMustScream.Stop(sound);
                    nerveSys.PlayedPainSounds.Remove(sound);
                }

                _IHaveNoMouthAndIMustScream.PlayPvs(nerveSys.PainScreams[sex], body);
                _popup.PopupPredicted(Loc.GetString("screams-and-flinches-pain", ("entity", body)), body, null, PopupType.MediumCaution);

                _jitter.DoJitter(body, TimeSpan.FromSeconds(0.9), true, 24f, 1f);

                break;
            case PainThresholdTypes.PainShock:
                foreach (var (sound, _) in nerveSys.PlayedPainSounds.Where(sound => !TerminatingOrDeleted(sound.Key)))
                {
                    _IHaveNoMouthAndIMustScream.Stop(sound);
                    nerveSys.PlayedPainSounds.Remove(sound);
                }

                _IHaveNoMouthAndIMustScream.PlayPvs(nerveSys.PainShockScreams[sex], body);
                _popup.PopupPredicted(Loc.GetString("screams-and-falls-pain", ("entity", body)), body, null, PopupType.MediumCaution);

                _stun.TryParalyze(body, nerveSys.PainShockStunTime, true);

                _IHaveNoMouthAndIMustScream.PlayPvs(nerveSys.PainShockWhimpers[sex], body);

                break;
            case PainThresholdTypes.PainPassout:
                foreach (var (sound, _) in nerveSys.PlayedPainSounds.Where(sound => !TerminatingOrDeleted(sound.Key)))
                {
                    _IHaveNoMouthAndIMustScream.Stop(sound);
                    nerveSys.PlayedPainSounds.Remove(sound);
                }

                _popup.PopupPredicted(Loc.GetString("passes-out-pain", ("entity", body)), body, null, PopupType.MediumCaution);
                _consciousness.ForcePassout(body, nerveSys.ForcePassoutTime);

                break;
            case PainThresholdTypes.None:
                break;
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

        if (!TryComp<OrganComponent>(uid, out var organ) || !organ.Body.HasValue)
            return;

        var ev1 = new PainThresholdTriggered(uid, nerveSys, nearestReflex, painInput);
        RaiseLocalEvent(organ.Body.Value, ref ev1);

        if (ev1.Cancelled || !_consciousness.CheckConscious(organ.Body.Value))
            return;

        var ev2 = new PainThresholdEffected(uid, nerveSys, nearestReflex, painInput);
        RaiseLocalEvent(organ.Body.Value, ref ev2);

        nerveSys.UpdateTime = _timing.CurTime + nerveSys.ThresholdUpdateTime;

        ApplyPainReflexesEffects(organ.Body.Value, nerveSys, nearestReflex);
        _sawmill.Info($"Pain threshold (reflex) chosen: {nearestReflex} ({nerveSys.PainThresholds[nearestReflex]}) with painInput of {painInput}. What a good day.");
    }

    private FixedPoint2 ApplyModifiersToPain(EntityUid nerveUid, FixedPoint2 pain, NerveSystemComponent nerveSys, NerveComponent? nerve = null)
    {
        if (!Resolve(nerveUid, ref nerve, false) || !_net.IsServer)
            return pain;

        var modifiedPain = pain * nerve.PainMultiplier;
        if (nerveSys.Multipliers.Count == 0)
            return modifiedPain;

        var toMultiply = nerveSys.Multipliers.Sum(multiplier => (int) multiplier.Value.Change);
        return modifiedPain * toMultiply / nerveSys.Multipliers.Count; // o(*^＠^*)o
    }

    #endregion
}
