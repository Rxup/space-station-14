using Content.Shared.Backmen.Surgery.Traumas.Systems;
using Content.Shared.Body.Part;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Utility;

namespace Content.Shared.Armor;

/// <summary>
/// Used for clothing that reduces damage when worn.
/// </summary>
[RegisterComponent, NetworkedComponent, Access(typeof(SharedArmorSystem), typeof(TraumaSystem))]
public sealed partial class ArmorComponent : Component
{
    /// <summary>
    /// The damage reduction
    /// </summary>
    [DataField(required: true)]
    public DamageModifierSet Modifiers = default!;

    /// <summary>
    /// A multiplier applied to the calculated point value
    /// to determine the monetary value of the armor
    /// </summary>
    [DataField]
    public float PriceMultiplier = 1;

    // thankfully all the armor in the game is symmetrical.
    [DataField("coverage")]
    public List<BodyPartType> ArmorCoverage = new();

    [DataField]
    public FixedPoint2 DismembermentChanceDeduction = 0;
}

/// <summary>
/// Event raised on an armor entity to get additional examine text relating to its armor.
/// </summary>
/// <param name="Msg"></param>
[ByRefEvent]
public record struct ArmorExamineEvent(FormattedMessage Msg);
