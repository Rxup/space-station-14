using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared.Backmen.CCVar;
using Content.Shared.Backmen.Surgery.Wounds;
using Content.Shared.Backmen.Surgery.Wounds.Components;
using Content.Shared.Backmen.Surgery.Wounds.Systems;
using Content.Shared.Backmen.Targeting;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Robust.Shared.Player;
using Robust.Shared.Threading;
using Robust.Shared.Timing;
using Robust.Shared.Toolshed.Commands.Math;

namespace Content.Server.Backmen.Surgery.Wounds.Systems;

public sealed class ServerWoundSystem : WoundSystem
{
    private float _medicalHealingTickrate = 0.5f;

    public override void Initialize()
    {
        base.Initialize();

        Subs.CVar(Cfg, CCVars.MedicalHealingTickrate, val => _medicalHealingTickrate = val, true);
    }

    public override bool TryHealWoundsOnWoundable(EntityUid woundable,
        FixedPoint2 healAmount,
        string damageType,
        out FixedPoint2 healed,
        WoundableComponent? component = null,
        bool ignoreMultipliers = false)
    {
        healed = 0;
        if (!Resolve(woundable, ref component) || component.Wounds == null)
            return false;

        var woundsToHeal =
            (from wound in component.Wounds.ContainedEntities
                let woundComp = Comp<WoundComponent>(wound)
                where CanHealWound(wound)
                where damageType == woundComp.DamageType
                select (wound, woundComp)).Select(dummy => (Entity<WoundComponent>) dummy)
            .ToList();

        if (woundsToHeal.Count == 0)
            return false;

        var healNumba = healAmount / woundsToHeal.Count;
        var actualHeal = FixedPoint2.Zero;
        foreach (var wound in woundsToHeal)
        {
            var heal = ignoreMultipliers
                ? ApplyHealingRateMultipliers((wound,wound), woundable, -healNumba, component)
                : -healNumba;

            actualHeal += -heal;
            ApplyWoundSeverity(wound, heal, wound);
        }

        UpdateWoundableIntegrity(woundable, component);
        CheckWoundableSeverityThresholds(woundable, component);

        healed = actualHeal;
        return actualHeal > 0;
    }

    public override bool TryHealWoundsOnWoundable(EntityUid woundable,
        FixedPoint2 healAmount,
        out FixedPoint2 healed,
        WoundableComponent? component = null,
        DamageGroupPrototype? damageGroup = null,
        bool ignoreMultipliers = false)
    {
        healed = 0;
        if (!Resolve(woundable, ref component) || component.Wounds == null)
            return false;

        var woundsToHeal =
            (from wound in component.Wounds.ContainedEntities
                let woundComp = Comp<WoundComponent>(wound)
                where CanHealWound(wound)
                where damageGroup == null || damageGroup == woundComp.DamageGroup
                select (wound, woundComp)).Select(dummy => (Entity<WoundComponent>) dummy)
            .ToList(); // that's what I call LINQ.

        if (woundsToHeal.Count == 0)
            return false;

        var healNumba = healAmount / woundsToHeal.Count;
        var actualHeal = FixedPoint2.Zero;
        foreach (var wound in woundsToHeal)
        {
            var heal = ignoreMultipliers
                ? ApplyHealingRateMultipliers((wound,wound), woundable, -healNumba, component)
                : -healNumba;

            actualHeal += -heal;
            ApplyWoundSeverity(wound, heal, wound);
        }

        UpdateWoundableIntegrity(woundable, component);
        CheckWoundableSeverityThresholds(woundable, component);

        healed = actualHeal;
        return actualHeal > 0;
    }

    private bool AddWound(
        EntityUid target,
        EntityUid wound,
        FixedPoint2 woundSeverity,
        DamageGroupPrototype? damageGroup,
        WoundableComponent? woundableComponent = null,
        WoundComponent? woundComponent = null)
    {
        if (!_woundableQuery.Resolve(target, ref woundableComponent, false)
            || !_woundQuery.Resolve(wound, ref woundComponent, false)
            || woundableComponent.Wounds == null
            || woundableComponent.Wounds.Contains(wound)
            )
            return false;

        if (woundSeverity <= _woundThresholds[WoundSeverity.Healed])
            return false;

        if (!woundableComponent.AllowWounds)
            return false;

        _transform.SetParent(wound, target);
        woundComponent.HoldingWoundable = target;
        woundComponent.DamageGroup = damageGroup;

        if (!_container.Insert(wound, woundableComponent.Wounds))
            return false;

        SetWoundSeverity(wound, woundSeverity, woundComponent);

        var woundMeta = MetaData(wound);
        var targetMeta = MetaData(target);

        Log.Debug($"Wound: {woundMeta.EntityPrototype!.ID}({wound}) created on {targetMeta.EntityPrototype!.ID}({target})");

        Dirty(wound, woundComponent);
        Dirty(target, woundableComponent);

        return true;
    }

    protected override bool RemoveWound(EntityUid woundEntity, WoundComponent? wound = null)
    {
        if (!_woundQuery.Resolve(woundEntity, ref wound, false) || !_woundableQuery.TryComp(wound.HoldingWoundable, out WoundableComponent? woundable))
            return false;

        Log.Debug($"Wound: {MetaData(woundEntity).EntityPrototype!.ID}({woundEntity}) removed on {MetaData(wound.HoldingWoundable).EntityPrototype!.ID}({wound.HoldingWoundable})");

        UpdateWoundableIntegrity(wound.HoldingWoundable, woundable);
        CheckWoundableSeverityThresholds(wound.HoldingWoundable, woundable);

        _container.Remove(woundEntity, woundable.Wounds!, false, true);
        return true;
    }

    public override bool TryCreateWound(
        EntityUid uid,
        string woundProtoId,
        FixedPoint2 severity,
        [NotNullWhen(true)] out Entity<WoundComponent>? woundCreated,
        DamageGroupPrototype? damageGroup,
        WoundableComponent? woundable = null)
    {
        woundCreated = null;
        if (!IsWoundPrototypeValid(woundProtoId))
            return false;

        if (!_woundableQuery.Resolve(uid, ref woundable))
            return false;

        var wound = Spawn(woundProtoId);
        if (AddWound(uid, wound, severity, damageGroup))
        {
            woundCreated = (wound, _woundQuery.Comp(wound));
        }
        else
        {
            // The wound failed some important checks, and we cannot let an invalid wound to be spawned!
            QueueDel(wound);
            return false;
        }

        return true;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var timeToHeal = 1 / _medicalHealingTickrate;
        using var query = EntityQueryEnumerator<WoundableComponent, MetaDataComponent>();
        while (query.MoveNext(out var ent, out var woundable, out var metaData))
        {
            if (Paused(ent, metaData))
                continue;

            woundable.HealingRateAccumulated += frameTime;
            if (woundable.HealingRateAccumulated < timeToHeal)
                continue;

            if (woundable.Wounds == null || woundable.Wounds.Count == 0)
                continue;

            woundable.HealingRateAccumulated -= timeToHeal;

            var woundsToHeal =
                GetWoundableWounds(ent, woundable).Where(wound => CanHealWound(wound, wound)).ToList();

            if (woundsToHeal.Count == 0)
                continue;

            var healAmount = -woundable.HealAbility / woundsToHeal.Count;

            Entity<WoundableComponent> owner = (ent, woundable);

            foreach (var x in woundsToHeal)
            {
                ApplyWoundSeverity(x,
                    ApplyHealingRateMultipliers((x,x), owner, healAmount, owner),
                    x);
            }
        }
    }
}
