using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared.Backmen.Damage;

public static class DamageSpecifierAliases
{
    public static DamageSpecifier ApplyDamageTypeAliases(DamageSpecifier spec, IPrototypeManager proto)
    {
        var newDamage = new DamageSpecifier(spec);

        foreach (var (type, value) in spec.DamageDict)
        {
            if (value == 0)
                continue;

            if (!proto.TryIndex<DamageTypePrototype>(type, out var typeProto) || typeProto.AppliesAs is not { } appliesAs)
                continue;

            newDamage.DamageDict.Remove(type);

            if (newDamage.DamageDict.TryGetValue(appliesAs, out var existing))
                newDamage.DamageDict[appliesAs] = existing + value;
            else
                newDamage.DamageDict[appliesAs] = value;
        }

        return newDamage;
    }

    public static bool IsPiercingDamageType(string type, IPrototypeManager proto)
    {
        if (type == "Piercing" || type == "ArmorPiercing")
            return true;

        return proto.TryIndex<DamageTypePrototype>(type, out var typeProto)
               && typeProto.AppliesAs == "Piercing";
    }
}
