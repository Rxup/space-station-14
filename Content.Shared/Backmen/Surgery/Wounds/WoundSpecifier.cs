using System.Linq;
using System.Text.Json.Serialization;
using Content.Shared.Backmen.Surgery.Wounds.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Backmen.Surgery.Wounds;

[DataDefinition, Serializable, NetSerializable]
public sealed partial class WoundSpecifier : IEquatable<WoundSpecifier>
{
    [JsonIgnore]
    [DataField(readOnly: true)]
    public Dictionary<EntProtoId<WoundComponent>, FixedPoint2> WoundDict { get; set; } = new();

    public FixedPoint2 GetTotal()
    {
        return WoundDict.Values.Aggregate(FixedPoint2.Zero, (current, value) => current + value);
    }

    [JsonIgnore]
    public bool Empty => WoundDict.Count == 0;

    #region Constructors

    public WoundSpecifier() {}

    public WoundSpecifier(Dictionary<EntProtoId<WoundComponent>, FixedPoint2> woundDict)
    {
        WoundDict = new(woundDict);
    }

    public WoundSpecifier(WoundSpecifier specifier)
    {
        WoundDict = new(specifier.WoundDict);
    }

    public WoundSpecifier(DamageSpecifier damageSpecifier, IPrototypeManager manager)
    {
        foreach (var damagePair in
                 damageSpecifier.DamageDict.Where(damagePair => manager.TryIndex<EntityPrototype>(damagePair.Key, out _)))
        {
            WoundDict.Add(damagePair.Key, damagePair.Value);
        }
    }

    /// <summary>
    ///     Constructor that takes a single damage type prototype and a damage value.
    /// </summary>
    public WoundSpecifier(DamageTypePrototype type, FixedPoint2 value)
    {
        WoundDict = new() { { type.ID, value } };
    }

    /// <summary>
    ///     Constructor that takes a single damage group prototype and a damage value. The value is divided between members of the damage group.
    /// </summary>
    public WoundSpecifier(DamageGroupPrototype group, FixedPoint2 value)
    {
        // Simply distribute evenly (except for rounding).
        // We do this by reducing remaining the # of types and damage every loop.
        var remainingTypes = group.DamageTypes.Count;
        var remainingDamage = value;
        foreach (var damageType in group.DamageTypes)
        {
            var damage = remainingDamage / FixedPoint2.New(remainingTypes);
            WoundDict.Add(damageType, damage);
            remainingDamage -= damage;
            remainingTypes -= 1;
        }
    }

    #endregion

    #region Operators

    public static WoundSpecifier operator *(WoundSpecifier specifier, FixedPoint2 factor)
    {
        WoundSpecifier newWounds = new();
        foreach (var entry in specifier.WoundDict)
        {
            newWounds.WoundDict.Add(entry.Key, entry.Value * factor);
        }
        return newWounds;
    }

    public static WoundSpecifier operator *(WoundSpecifier specifier, float factor)
    {
        WoundSpecifier newWounds = new();
        foreach (var entry in specifier.WoundDict)
        {
            newWounds.WoundDict.Add(entry.Key, entry.Value * factor);
        }
        return newWounds;
    }

    public static WoundSpecifier operator /(WoundSpecifier specifier, FixedPoint2 factor)
    {
        WoundSpecifier newWounds = new();
        foreach (var entry in specifier.WoundDict)
        {
            newWounds.WoundDict.Add(entry.Key, entry.Value / factor);
        }
        return newWounds;
    }

    public static WoundSpecifier operator /(WoundSpecifier specifier, float factor)
    {
        WoundSpecifier newWounds = new();
        foreach (var entry in specifier.WoundDict)
        {
            newWounds.WoundDict.Add(entry.Key, entry.Value / factor);
        }
        return newWounds;
    }

    public static WoundSpecifier operator +(WoundSpecifier specifierA, WoundSpecifier specifierB)
    {
        WoundSpecifier newWounds = new(specifierA);

        foreach (var entry in specifierB.WoundDict)
        {
            if (!newWounds.WoundDict.TryAdd(entry.Key, entry.Value))
            {
                newWounds.WoundDict[entry.Key] += entry.Value;
            }
        }
        return newWounds;
    }

    public static WoundSpecifier operator -(WoundSpecifier specifierA, WoundSpecifier specifierB)
    {
        WoundSpecifier newWounds = new(specifierA);

        foreach (var entry in specifierB.WoundDict)
        {
            if (!newWounds.WoundDict.TryAdd(entry.Key, -entry.Value))
            {
                newWounds.WoundDict[entry.Key] -= entry.Value;
            }
        }
        return newWounds;
    }

    public static WoundSpecifier operator +(WoundSpecifier specifier) => specifier;

    public static WoundSpecifier operator -(WoundSpecifier specifier) => specifier * -1;

    public static WoundSpecifier operator *(float factor, WoundSpecifier specifier) => specifier * factor;

    public static WoundSpecifier operator *(FixedPoint2 factor, WoundSpecifier specifier) => specifier * factor;

    public bool Equals(WoundSpecifier? other)
    {
        if (other == null || WoundDict.Count != other.WoundDict.Count)
            return false;

        foreach (var (key, value) in WoundDict)
        {
            if (!other.WoundDict.TryGetValue(key, out var otherValue) || value != otherValue)
                return false;
        }

        return true;
    }

    public FixedPoint2 this[string key] => WoundDict[key];

    #endregion
}
