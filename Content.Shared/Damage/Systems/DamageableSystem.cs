using System.Linq;
using Content.Shared.Backmen.Surgery.Consciousness.Components;
using Content.Shared.Backmen.Surgery.Wounds;
using Content.Shared.Backmen.Surgery.Wounds.Components;
using Content.Shared.Backmen.Surgery.Wounds.Systems;
using Content.Shared.CCVar;
using Content.Shared.Chemistry;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Content.Shared.Inventory;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Body.Systems;
using Content.Shared.Radiation.Events;
using Content.Shared.Rejuvenate;
using Content.Shared.Backmen.Targeting;
using Content.Shared.Body.Components;
using Content.Shared.Damage.Components;
using Content.Shared.Explosion.EntitySystems;
using Robust.Shared.Configuration;
using Robust.Shared.GameStates;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Shared.Damage.Systems;

public sealed partial class DamageableSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly INetManager _netMan = default!;
    [Dependency] private readonly MobThresholdSystem _mobThreshold = default!;
    [Dependency] private readonly SharedBodySystem _body = default!;
    [Dependency] private readonly IConfigurationManager _config = default!;
    [Dependency] private readonly SharedChemistryGuideDataSystem _chemistryGuideData = default!;
    [Dependency] private readonly SharedExplosionSystem _explosion = default!;

    // backmen edit start
    [Dependency] private readonly WoundSystem _wounds = default!;
    // backmen edit end

    private EntityQuery<AppearanceComponent> _appearanceQuery;
    private EntityQuery<DamageableComponent> _damageableQuery;

    public float UniversalAllDamageModifier { get; private set; } = 1f;
    public float UniversalAllHealModifier { get; private set; } = 1f;
    public float UniversalMeleeDamageModifier { get; private set; } = 1f;
    public float UniversalProjectileDamageModifier { get; private set; } = 1f;
    public float UniversalHitscanDamageModifier { get; private set; } = 1f;
    public float UniversalReagentDamageModifier { get; private set; } = 1f;
    public float UniversalReagentHealModifier { get; private set; } = 1f;
    public float UniversalExplosionDamageModifier { get; private set; } = 1f;
    public float UniversalThrownDamageModifier { get; private set; } = 1f;
    public float UniversalTopicalsHealModifier { get; private set; } = 1f;
    public float UniversalMobDamageModifier { get; private set; } = 1f;

    public void SetDamage(EntityUid uid, DamageableComponent damageable, DamageSpecifier damage)
    {
        damageable.Damage = damage;
        OnEntityDamageChanged((uid, damageable));
    }

    /// <summary>
    ///     Called whenever damage on an entity actually changes.
    /// </summary>
    private void OnEntityDamageChanged(
        Entity<DamageableComponent> ent,
        DamageSpecifier? damageDelta = null,
        bool interruptsDoAfters = true,
        EntityUid? origin = null)
    {
        ent.Comp.Damage.GetDamagePerGroup(_prototypeManager, ent.Comp.DamagePerGroup);
        ent.Comp.TotalDamage = ent.Comp.Damage.GetTotal();
        Dirty(ent);

        if (damageDelta != null && _appearanceQuery.TryGetComponent(ent, out var appearance))
        {
            _appearance.SetData(
                ent,
                DamageVisualizerKeys.DamageUpdateGroups,
                new DamageVisualizerGroupData(ent.Comp.DamagePerGroup.Keys.ToList()),
                appearance);
        }

        RaiseLocalEvent(ent, new DamageChangedEvent(ent.Comp, damageDelta, interruptsDoAfters, origin));
    }

    public void SetDamageModifierSetId(EntityUid uid, string? damageModifierSetId, DamageableComponent? comp = null)
    {
        if (!_damageableQuery.Resolve(uid, ref comp))
            return;

        comp.DamageModifierSetId = damageModifierSetId;
        Dirty(uid, comp);
    }

    private void DamageableGetState(Entity<DamageableComponent> ent, ref ComponentGetState args)
    {
        if (_netMan.IsServer)
        {
            args.State = new DamageableComponentState(
                ent.Comp.Damage.DamageDict,
                ent.Comp.DamageContainerID,
                ent.Comp.DamageModifierSetId,
                ent.Comp.HealthBarThreshold);
            return;
        }

        args.State = new DamageableComponentState(
            ent.Comp.Damage.DamageDict.ShallowClone(),
            ent.Comp.DamageContainerID,
            ent.Comp.DamageModifierSetId,
            ent.Comp.HealthBarThreshold);
    }
}
