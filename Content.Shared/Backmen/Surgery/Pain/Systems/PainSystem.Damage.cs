using System.Linq;
using Content.Shared.Backmen.Surgery.Pain.Components;
using Content.Shared.Backmen.Surgery.Wounds;
using Content.Shared.FixedPoint;

namespace Content.Shared.Backmen.Surgery.Pain.Systems;

public partial class PainSystem
{
    #region Data

    private readonly Dictionary<WoundSeverity, FixedPoint2> _painMultipliers = new()
    {
        { WoundSeverity.Healed, 1 },
        { WoundSeverity.Minor, 1 },
        { WoundSeverity.Moderate, 1.15 },
        { WoundSeverity.Severe, 1.35 },
        { WoundSeverity.Critical, 1.5 },
        { WoundSeverity.Loss, 1.75},
    };

    #endregion

    #region Public API

    /// <summary>
    /// Change pain for specific nerve, if there's any. Adds MORE PAIN to it basically.
    /// </summary>
    /// <param name="uid">Uid of the nerveSystem component owner.</param>
    /// <param name="nerveUid">Nerve uid.</param>
    /// <param name="change">How many pain to add.</param>
    /// <param name="nerveSys">NerveSystemComponent.</param>
    /// <returns>Returns true, if PAIN QUOTA WAS COLLECTED.</returns>
    public bool TryChangePainModifier(EntityUid uid, EntityUid nerveUid, FixedPoint2 change, NerveSystemComponent? nerveSys = null)
    {
        if (!Resolve(uid, ref nerveSys) || _net.IsClient)
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

        if (!Resolve(uid, ref nerveSys))
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
        if (!Resolve(uid, ref nerveSys) || _net.IsClient)
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
        if (!Resolve(uid, ref nerveSys) || _net.IsClient)
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
        if (!Resolve(uid, ref nerveSys) || _net.IsClient)
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
        if (!Resolve(uid, ref nerveSys) || _net.IsClient)
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
        if (!Resolve(uid, ref nerveSys) || _net.IsClient)
            return false;

        if (!nerveSys.Multipliers.Remove(identifier))
            return false;

        UpdateNerveSystemPain(uid, nerveSys);

        Dirty(uid, nerveSys);
        return true;
    }

    public Entity<NerveSystemComponent>? GetNerveSystem(EntityUid? body)
    {
        foreach (var (id, _) in _body.GetBodyOrgans(body))
        {
            if (TryComp<NerveSystemComponent>(id, out var component))
                return (id, component);
        }

        return null;
    }

    #endregion

    #region Private API

    private void UpdateNerveSystemPain(EntityUid uid, NerveSystemComponent? nerveSys = null)
    {
        if (!Resolve(uid, ref nerveSys) || !_net.IsServer)
            return;

        nerveSys.Pain =
            FixedPoint2.Clamp(
                nerveSys.Modifiers.Aggregate((FixedPoint2) 0,
                    (current, modifier) =>
                    current + ApplyModifiersToPain(modifier.Key, modifier.Value.Change, nerveSys)),
                0,
                100);
    }

    private FixedPoint2 ApplyModifiersToPain(EntityUid nerveUid, FixedPoint2 pain, NerveSystemComponent nerveSys, NerveComponent? nerve = null)
    {
        if (!Resolve(nerveUid, ref nerve) || !_net.IsServer)
            return pain;

        var modifiedPain = pain * nerve.PainMultiplier;
        if (nerveSys.Multipliers.Count == 0)
            return modifiedPain;

        var toMultiply = nerveSys.Multipliers.Sum(multiplier => (int) multiplier.Value.Change);
        return modifiedPain * toMultiply / nerveSys.Multipliers.Count; // o(*^＠^*)o
    }

    #endregion
}
