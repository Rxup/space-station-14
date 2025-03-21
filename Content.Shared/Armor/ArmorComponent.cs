using Content.Shared.Backmen.Surgery.Traumas;
using Content.Shared.Backmen.Surgery.Traumas.Systems;
using Content.Shared.Body.Part;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Damage;
using Content.Shared.Inventory;
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

    /// <summary>
    /// If true, the coverage won't show.
    /// </summary>
    [DataField("coverageHidden")]
    public bool ArmourCoverageHidden = false;

    /// <summary>
    /// If true, the modifiers won't show.
    /// </summary>
    [DataField("modifiersHidden")]
    public bool ArmourModifiersHidden = false;

    // thankfully all the armor in the game is symmetrical.
    [DataField("coverage")]
    public List<BodyPartType> ArmorCoverage = new();

    [DataField]
    public Dictionary<TraumaType, FixedPoint2> TraumaDeductions = new()
    {
        { TraumaType.Dismemberment, 0 },
        { TraumaType.BoneDamage, 0 },
        { TraumaType.OrganDamage, 0 },
        { TraumaType.VeinsDamage, 0 },
        { TraumaType.NerveDamage, 0 },
    };
}

/// <summary>
/// Event raised on an armor entity to get additional examine text relating to its armor.
/// </summary>
/// <param name="Msg"></param>
[ByRefEvent]
public record struct ArmorExamineEvent(FormattedMessage Msg);

/// <summary>
/// A Relayed inventory event, gets the total Armor for all Inventory slots defined by the Slotflags in TargetSlots
/// </summary>
public sealed class CoefficientQueryEvent : EntityEventArgs, IInventoryRelayEvent
{
    /// <summary>
    /// All slots to relay to
    /// </summary>
    public SlotFlags TargetSlots { get; set; }

    /// <summary>
    /// The Total of all Coefficients.
    /// </summary>
    public DamageModifierSet DamageModifiers { get; set; } = new DamageModifierSet();

    public CoefficientQueryEvent(SlotFlags slots)
    {
        TargetSlots = slots;
    }
}
