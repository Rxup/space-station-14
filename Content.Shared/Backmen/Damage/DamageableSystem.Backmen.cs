using Content.Shared.Backmen.Body.Systems;
using Content.Shared.Backmen.Surgery.Consciousness.Components;
using Content.Shared.Backmen.Surgery.Wounds;
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
    private void OnWoundableIntegrityChangedOnBody(
        Entity<ConsciousnessComponent> ent,
        ref WoundableIntegrityChangedOnBodyEvent args)
    {
        if (!_netMan.IsServer || !TryComp<DamageableComponent>(ent, out var damageable))
            return;

        SyncDamageableFromBodyWounds((ent, damageable), ent.Comp.DamageContainer);
    }

    /// <summary>
    /// Keeps <see cref="DamageableComponent"/> aligned with wound severity after direct wound healing
    /// (medical items, surgery) that bypasses <see cref="ChangeDamage"/>.
    /// </summary>
    public void SyncDamageableFromBodyWounds(
        Entity<DamageableComponent> ent,
        ProtoId<DamageContainerPrototype>? container)
    {
        if (!TryComp<ConsciousnessComponent>(ent, out _))
            return;

        var woundDamage = _wounds.GetBodyWoundDamageSpecifier(ent);
        var delta = new DamageSpecifier();

        foreach (var (type, amount) in woundDamage.DamageDict)
        {
            ent.Comp.Damage.DamageDict.TryGetValue(type, out var current);
            var diff = amount - current;
            if (diff != FixedPoint2.Zero)
                delta.DamageDict[type] = diff;
        }

        foreach (var (type, current) in ent.Comp.Damage.DamageDict)
        {
            if (current <= FixedPoint2.Zero || woundDamage.DamageDict.ContainsKey(type))
                continue;

            if (!_backmenDamageModel.CanBeDamagedBy((ent.Owner, ent.Comp), type))
                continue;

            delta.DamageDict[type] = -current;
        }

        if (!delta.Empty)
            ApplyDamageToDamageable(ent, delta, container, null, false);
    }

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
