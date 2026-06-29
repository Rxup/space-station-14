using Content.Shared.Backmen.Body.Systems;
using Content.Shared.Backmen.Surgery.Consciousness.Components;
using Content.Shared.Backmen.Surgery.Wounds.Components;
using Content.Shared.Backmen.Surgery.Wounds.Systems;
using Content.Shared.Body;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Shared.Damage.Systems;

public sealed partial class DamageableSystem
{
    public void ApplyDamageToDamageable(
        Entity<DamageableComponent> ent,
        DamageSpecifier damage,
        ProtoId<DamageContainerPrototype>? container,
        EntityUid? origin,
        bool interruptsDoAfters)
    {
        var damageDone = new DamageSpecifier();
        damageDone.DamageDict.EnsureCapacity(damage.DamageDict.Count);

        var dict = ent.Comp.Damage.DamageDict;
        foreach (var (type, value) in damage.DamageDict)
        {
            if (!SupportsType(container, type))
                continue;

            dict.TryGetValue(type, out var oldValue);

            var newValue = FixedPoint2.Max(FixedPoint2.Zero, oldValue + value);
            if (newValue == oldValue)
                continue;

            dict[type] = newValue;
            damageDone.DamageDict[type] = newValue - oldValue;
        }

        if (!damageDone.Empty)
            OnEntityDamageChanged(ent, damageDone, interruptsDoAfters, origin);
    }

    internal bool SupportsDamageType(ProtoId<DamageContainerPrototype>? container, ProtoId<DamageTypePrototype> type) =>
        SupportsType(container, type);

    private void SyncBackmenWoundSeverityFromSetAllDamage(Entity<DamageableComponent?> ent, FixedPoint2 newValue)
    {
        if (!TryComp<BodyComponent>(ent, out var body) || !TryComp<ConsciousnessComponent>(ent, out _))
            return;

        foreach (var part in _body.GetDistributedDamageTargets(ent.Owner, body))
        {
            if (!TryComp(part, out WoundableComponent? woundable))
                continue;

            foreach (var wound in _wounds.GetWoundableWounds(part, woundable))
            {
                _wounds.SetWoundSeverity(wound, newValue, wound);
            }
        }
    }
}
