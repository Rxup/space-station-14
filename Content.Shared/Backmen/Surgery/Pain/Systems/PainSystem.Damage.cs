using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared.Backmen.Surgery.Pain.Components;
using Content.Shared.FixedPoint;
using JetBrains.Annotations;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;

namespace Content.Shared.Backmen.Surgery.Pain.Systems;

public partial class PainSystem
{
    #region Public API

    public void CleanupSounds(NerveSystemComponent nerveSys)
    {
        foreach (var (id, _) in nerveSys.PlayedPainSounds.Where(sound => !TerminatingOrDeleted(sound.Key)))
        {
            IHaveNoMouthAndIMustScream.Stop(id);
            nerveSys.PlayedPainSounds.Remove(id);
        }

        foreach (var (id, _) in nerveSys.PainSoundsToPlay.Where(sound => !TerminatingOrDeleted(sound.Key)))
        {
            nerveSys.PainSoundsToPlay.Remove(id);
        }
    }

    public Entity<AudioComponent>? PlayPainSound(EntityUid body, SoundSpecifier specifier, AudioParams? audioParams = null)
    {
        return IHaveNoMouthAndIMustScream.PlayPvs(specifier, body, audioParams);
    }

    public Entity<AudioComponent>? PlayPainSound(EntityUid body, NerveSystemComponent nerveSys, SoundSpecifier specifier, AudioParams? audioParams = null)
    {
        var sound = IHaveNoMouthAndIMustScream.PlayPvs(specifier, body, audioParams);
        if (!sound.HasValue)
            return null;

        nerveSys.PlayedPainSounds.Add(sound.Value.Entity, sound.Value.Component);
        return sound.Value;
    }

    public Entity<AudioComponent>? PlayPainSoundWithCleanup(EntityUid body, NerveSystemComponent nerveSys, SoundSpecifier specifier, AudioParams? audioParams = null)
    {
        CleanupSounds(nerveSys);
        var sound = IHaveNoMouthAndIMustScream.PlayPvs(specifier, body, audioParams);
        if (!sound.HasValue)
            return null;

        nerveSys.PlayedPainSounds.Add(sound.Value.Entity, sound.Value.Component);
        return sound.Value;
    }

    public void PlayPainSound(EntityUid body, NerveSystemComponent nerveSys, SoundSpecifier specifier, TimeSpan delay, AudioParams? audioParams = null)
    {
        nerveSys.PainSoundsToPlay.Add(body, (specifier, audioParams, Timing.CurTime + delay));
    }

    [PublicAPI]
    public FixedPoint2 ApplyModifiersToPain(
        EntityUid nerveUid,
        FixedPoint2 pain,
        NerveSystemComponent nerveSys,
        PainDamageTypes painType,
        NerveComponent? nerve = null)
    {
        if (!NerveQuery.Resolve(nerveUid, ref nerve, false))
            return pain;

        var modifiedPain = pain * nerve.PainMultiplier;
        if (nerveSys.Multipliers.Count == 0)
            return modifiedPain;

        var toMultiply =
            nerveSys.Multipliers
                .Where(markiplier => markiplier.Value.PainDamageType == painType)
                .Aggregate(FixedPoint2.Zero, (current, markiplier) => current + markiplier.Value.Change);

        return modifiedPain * toMultiply / nerveSys.Multipliers.Count; // o(*^＠^*)o
    }

    /// <summary>
    /// Changes a pain value for a specific nerve, if there's any. Adds MORE PAIN to it basically.
    /// </summary>
    /// <param name="uid">Uid of the nerveSystem component owner.</param>
    /// <param name="nerveUid">Nerve uid.</param>
    /// <param name="identifier">Identifier of the said modifier.</param>
    /// <param name="change">How many pain to set.</param>
    /// <param name="nerveSys">NerveSystemComponent.</param>
    /// <param name="time">How long will the modifier last?</param>
    /// <param name="painType">The damage type of pain</param>
    /// <returns>Returns true, if PAIN QUOTA WAS COLLECTED.</returns>
    [PublicAPI]
    public virtual bool TryChangePainModifier(
        EntityUid uid,
        EntityUid nerveUid,
        string identifier,
        FixedPoint2 change,
        NerveSystemComponent? nerveSys = null,
        TimeSpan? time = null,
        PainDamageTypes? painType = null)
    {
        // Server-only execution
        return false;
    }

    /// <summary>
    /// Gets a copy of pain modifier.
    /// </summary>
    /// <param name="uid">Uid of the nerveSystem component owner.</param>
    /// <param name="nerveUid">Nerve uid, used to seek for modifier.</param>
    /// <param name="identifier">Identifier of the said modifier.</param>
    /// <param name="modifier">Modifier copy acquired.</param>
    /// <param name="nerveSys">NerveSystemComponent.</param>
    /// <returns>Returns true, if the modifier was acquired.</returns>
    [PublicAPI]
    public bool TryGetPainModifier(
        EntityUid uid,
        EntityUid nerveUid,
        string identifier,
        [NotNullWhen(true)] out PainModifier? modifier,
        NerveSystemComponent? nerveSys = null)
    {
        modifier = null;
        if (!Resolve(uid, ref nerveSys, false))
            return false;

        if (!nerveSys.Modifiers.TryGetValue((nerveUid, identifier), out var data))
            return false;

        modifier = data;
        return true;
    }

    /// <summary>
    /// Adds pain to needed nerveSystem, uses modifiers.
    /// </summary>
    /// <param name="uid">Uid of the nerveSystem owner.</param>
    /// <param name="nerveUid">Uid of the nerve, to which damage was applied.</param>
    /// <param name="identifier">Identifier of the said modifier.</param>
    /// <param name="change">Number of pain to add.</param>
    /// <param name="painType">Damage type for pain</param>
    /// <param name="nerveSys">NerveSystem component.</param>
    /// <param name="time">Timespan of the modifier's existence</param>
    /// <returns>Returns true, if the PAIN WAS APPLIED.</returns>
    [PublicAPI]
    public virtual bool TryAddPainModifier(
        EntityUid uid,
        EntityUid nerveUid,
        string identifier,
        FixedPoint2 change,
        PainDamageTypes painType = PainDamageTypes.WoundPain,
        NerveSystemComponent? nerveSys = null,
        TimeSpan? time = null)
    {
        // Server-only execution
        return false;
    }

    /// <summary>
    /// Adds a pain feeling modifier to the needed nerve, uses modifiers.
    /// </summary>
    /// <param name="effectOwner">Uid of the owner of this effect.</param>
    /// <param name="identifier">The string identifier of the modifier to add</param>
    /// <param name="nerveUid">Uid of the nerve, to which damage is being applied.</param>
    /// <param name="change">Number of pain feeling to add / subtract.</param>
    /// <param name="nerve">The nerve component of the nerve entity.</param>
    /// <param name="time">The TimeSpan of the effect; When runs out, the effect will be removed.</param>
    /// <returns>Returns true, if the pain feeling modifier was added.</returns>
    [PublicAPI]
    public virtual bool TryAddPainFeelsModifier(
        EntityUid effectOwner,
        string identifier,
        EntityUid nerveUid,
        FixedPoint2 change,
        NerveComponent? nerve = null,
        TimeSpan? time = null)
    {
        // Server-only execution
        return false;
    }

    /// <summary>
    /// Tries to get a pain feeling modifier.
    /// </summary>
    /// <param name="nerveEnt">Uid of the nerve from which you get the modifier.</param>
    /// <param name="effectOwner">Uid of the effect owner.</param>
    /// <param name="identifier">String identifier of the modifier.</param>
    /// <param name="modifier">The modifier you wanted.</param>
    /// <param name="nerve">The nerve component of the nerve entity.</param>
    /// <returns>Returns true, if the pain feeling modifier was added.</returns>
    [PublicAPI]
    public bool TryGetPainFeelsModifier(EntityUid nerveEnt,
        EntityUid effectOwner,
        string identifier,
        [NotNullWhen(true)] out PainFeelingModifier? modifier,
        NerveComponent? nerve = null)
    {
        modifier = null;
        if (!Resolve(nerveEnt, ref nerve, false))
            return false;

        if (!nerve.PainFeelingModifiers.TryGetValue((effectOwner, identifier), out var data))
            return false;

        modifier = data;
        return true;
    }

    /// <summary>
    /// Changes a pain feeling modifier of a needed nerve, uses modifiers.
    /// </summary>
    /// <param name="effectOwner">Uid of the owner of this effect.</param>
    /// <param name="identifier">The string identifier of this.. yeah</param>
    /// <param name="nerveUid">Uid of the nerve, to which damage is being applied.</param>
    /// <param name="change">Number of pain feeling to add / subtract.</param>
    /// <param name="nerve">The nerve component of the nerve entity.</param>
    /// <returns>Returns true, if the pain feeling modifier was changed.</returns>
    [PublicAPI]
    public virtual bool TryChangePainFeelsModifier(
        EntityUid effectOwner,
        string identifier,
        EntityUid nerveUid,
        FixedPoint2 change,
        NerveComponent? nerve = null)
    {
        // Server-only execution
        return false;
    }

    /// <summary>
    /// Sets a pain feeling modifier of a needed nerve, uses modifiers.
    /// </summary>
    /// <param name="effectOwner">Uid of the owner of this effect.</param>
    /// <param name="identifier">The string identifier of this.. yeah</param>
    /// <param name="nerveUid">Uid of the nerve, to which damage is being applied.</param>
    /// <param name="change">Number of pain feeling to add / subtract.</param>
    /// <param name="nerve">The nerve component of the nerve entity.</param>
    /// <param name="time">The TimeSpan of the effect; When runs out, the effect will be removed.</param>
    /// <returns>Returns true, if the pain feeling modifier was changed.</returns>
    [PublicAPI]
    public virtual bool TrySetPainFeelsModifier(
        EntityUid effectOwner,
        string identifier,
        EntityUid nerveUid,
        FixedPoint2 change,
        TimeSpan? time = null,
        NerveComponent? nerve = null)
    {
        // Server-only execution
        return false;
    }

    /// <summary>
    /// Sets a pain feeling modifier of a needed nerve, uses modifiers.
    /// </summary>
    /// <param name="effectOwner">Uid of the owner of this effect.</param>
    /// <param name="identifier">The string identifier of this.. yeah</param>
    /// <param name="nerveUid">Uid of the nerve, to which damage is being applied.</param>
    /// <param name="change">Number of pain feeling to add / subtract.</param>
    /// <param name="nerve">The nerve component of the nerve entity.</param>
    /// <param name="time">The TimeSpan of the effect; When runs out, the effect will be removed.</param>
    /// <returns>Returns true, if the pain feeling modifier was changed.</returns>
    [PublicAPI]
    public virtual bool TrySetPainFeelsModifier(
        EntityUid effectOwner,
        string identifier,
        EntityUid nerveUid,
        TimeSpan time,
        NerveComponent? nerve = null,
        FixedPoint2? change = null)
    {
        // Server-only execution
        return false;
    }

    /// <summary>
    /// Removes a pain feeling modifier of a needed nerve, uses modifiers.
    /// </summary>
    /// <param name="effectOwner">Uid of the owner of this effect.</param>
    /// <param name="identifier">The identifier of the said modifier.</param>
    /// <param name="nerveUid">Uid of the nerve, to which damage is being applied.</param>
    /// <param name="nerve">The nerve component of the nerve entity.</param>
    /// <returns>Returns true, if the pain feeling modifier was removed.</returns>
    [PublicAPI]
    public virtual bool TryRemovePainFeelsModifier(
        EntityUid effectOwner,
        string identifier,
        EntityUid nerveUid,
        NerveComponent? nerve = null)
    {
        // Server-only execution
        return false;
    }

    /// <summary>
    /// Removes a specified pain modifier.
    /// </summary>
    /// <param name="uid">NerveSystemComponent owner.</param>
    /// <param name="nerveUid">Nerve Uid, to which pain is applied.</param>
    /// <param name="identifier">Identifier of the said pain modifier.</param>
    /// <param name="nerveSys">NerveSystemComponent.</param>
    /// <returns>Returns true, if the pain modifier was removed.</returns>
    [PublicAPI]
    public virtual bool TryRemovePainModifier(
        EntityUid uid,
        EntityUid nerveUid,
        string identifier,
        NerveSystemComponent? nerveSys = null)
    {
        // Server-only execution
        return false;
    }

    /// <summary>
    /// Adds pain multiplier to nerveSystem.
    /// </summary>
    /// <param name="uid">NerveSystem owner's uid.</param>
    /// <param name="identifier">ID for the multiplier.</param>
    /// <param name="change">Number to multiply.</param>
    /// <param name="painType">Damage type of pain</param>
    /// <param name="nerveSys">NerveSystemComponent.</param>
    /// <param name="time">A timer for this multiplier; Upon it's end, the multiplier gets removed.</param>
    /// <returns>Returns true, if the multiplier was applied.</returns>
    [PublicAPI]
    public virtual bool TryAddPainMultiplier(
        EntityUid uid,
        string identifier,
        FixedPoint2 change,
        PainDamageTypes painType = PainDamageTypes.WoundPain,
        NerveSystemComponent? nerveSys = null,
        TimeSpan? time = null)
    {
        // Server-only execution
        return false;
    }


    /// <summary>
    /// Changes an existing pain multiplier's data, on a specified nerve system.
    /// </summary>
    /// <param name="uid">NerveSystem owner's uid.</param>
    /// <param name="identifier">ID for the multiplier.</param>
    /// <param name="change">Number to multiply.</param>
    /// <param name="nerveSys">NerveSystemComponent.</param>
    /// <param name="time">For how long will be this multiplier applied?</param>
    /// <param name="painType">Damage type of pain</param>
    /// <returns>Returns true, if the multiplier was changed.</returns>
    [PublicAPI]
    public virtual bool TryChangePainMultiplier(
        EntityUid uid,
        string identifier,
        FixedPoint2 change,
        TimeSpan? time = null,
        PainDamageTypes? painType = null,
        NerveSystemComponent? nerveSys = null)
    {
        // Server-only execution
        return false;
    }

    /// <summary>
    /// Changes an existing pain multiplier's data, on a specified nerve system.
    /// </summary>
    /// <param name="uid">NerveSystem owner's uid.</param>
    /// <param name="identifier">ID for the multiplier.</param>
    /// <param name="change">Number to multiply.</param>
    /// <param name="nerveSys">NerveSystemComponent.</param>
    /// <param name="time">For how long will be this multiplier applied?</param>
    /// <param name="painType">Damage type of pain</param>
    /// <returns>Returns true, if the multiplier was changed.</returns>
    [PublicAPI]
    public virtual bool TryChangePainMultiplier(
        EntityUid uid,
        string identifier,
        TimeSpan time,
        FixedPoint2? change = null,
        PainDamageTypes? painType = null,
        NerveSystemComponent? nerveSys = null)
    {
        // Server-only execution
        return false;
    }

    /// <summary>
    /// Changes an existing pain multiplier's data, on a specified nerve system.
    /// </summary>
    /// <param name="uid">NerveSystem owner's uid.</param>
    /// <param name="identifier">ID for the multiplier.</param>
    /// <param name="change">Number to multiply.</param>
    /// <param name="nerveSys">NerveSystemComponent.</param>
    /// <param name="time">For how long will be this multiplier applied?</param>
    /// <param name="painType">Damage type of pain</param>
    /// <returns>Returns true, if the multiplier was changed.</returns>
    [PublicAPI]
    public virtual bool TryChangePainMultiplier(
        EntityUid uid,
        string identifier,
        PainDamageTypes painType,
        FixedPoint2? change = null,
        TimeSpan? time = null,
        NerveSystemComponent? nerveSys = null)
    {
        // Server-only execution
        return false;
    }

    /// <summary>
    /// Removes a pain multiplier.
    /// </summary>
    /// <param name="uid">NerveSystem owner's uid.</param>
    /// <param name="identifier">ID to seek for the multiplier, what must be removed.</param>
    /// <param name="nerveSys">NerveSystemComponent.</param>
    /// <returns>Returns true, if the multiplier was removed.</returns>
    public virtual bool TryRemovePainMultiplier(EntityUid uid, string identifier, NerveSystemComponent? nerveSys = null)
    {
        // Server-only execution
        return false;
    }

    #endregion
}
