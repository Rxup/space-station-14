using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared.Backmen.CCVar;
using Content.Shared.Backmen.Surgery.Wounds;
using Content.Shared.Backmen.Surgery.Wounds.Components;
using Content.Shared.Backmen.Surgery.Wounds.Systems;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Backmen.Surgery.Wounds.Systems;

public sealed class ServerWoundSystem : WoundSystem
{
    private float _medicalHealingTickrate = 0.5f;
    private float _woundScarChance;
    private float _maxWoundSeverity;

    [Dependency] private readonly IPrototypeManager _prototype = default!;

    public override void Initialize()
    {
        base.Initialize();

        Subs.CVar(Cfg, CCVars.MedicalHealingTickrate, val => _medicalHealingTickrate = val, true);
        Subs.CVar(Cfg, CCVars.WoundScarChance, val => _woundScarChance = val, true);
        Subs.CVar(Cfg, CCVars.MaxWoundSeverity, val => _maxWoundSeverity = val, true);
    }

    #region Helpers, Wound Inducing

    [PublicAPI]
    public override bool TryInduceWounds(
        EntityUid uid,
        WoundSpecifier wounds,
        out List<Entity<WoundComponent>> woundsInduced,
        WoundableComponent? woundable = null)
    {
        woundsInduced = new List<Entity<WoundComponent>>();
        if (!_woundableQuery.Resolve(uid, ref woundable))
            return false;

        foreach (var woundToInduce in wounds.WoundDict)
        {
            if (!TryInduceWound(uid, woundToInduce.Key, woundToInduce.Value, out var woundInduced, woundable))
                return false;

            woundsInduced.Add(woundInduced.Value);
        }

        return true;
    }

    [PublicAPI]
    public override bool TryInduceWound(
        EntityUid uid,
        string woundId,
        FixedPoint2 severity,
        [NotNullWhen(true)] out Entity<WoundComponent>? woundInduced,
        WoundableComponent? woundable = null)
    {
        woundInduced = null;
        if (!_woundableQuery.Resolve(uid, ref woundable))
            return false;

        if (TryContinueWound(uid, woundId, severity, out woundInduced, woundable))
            return true;

        return TryCreateWound(
            uid,
            woundId,
            severity,
            out woundInduced,
            (from @group in _prototype.EnumeratePrototypes<DamageGroupPrototype>()
                where @group.DamageTypes.Contains(woundId)
                select @group).FirstOrDefault(),
            woundable);
    }

    [PublicAPI]
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

    [PublicAPI]
    public override bool TryContinueWound(
        EntityUid uid,
        string id,
        FixedPoint2 severity,
        [NotNullWhen(true)] out Entity<WoundComponent>? woundContinued,
        WoundableComponent? woundable = null)
    {
        woundContinued = null;
        if (!IsWoundPrototypeValid(id))
            return false;

        if (!_woundableQuery.Resolve(uid, ref woundable))
            return false;

        var proto = _prototype.Index(id);
        foreach (var wound in GetWoundableWounds(uid, woundable))
        {
            if (proto.ID != wound.Comp.DamageType)
                continue;

            ApplyWoundSeverity(wound, severity, wound);
            woundContinued = wound;

            return true;
        }

        return false;
    }

    [PublicAPI]
    public override bool TryMakeScar(
        EntityUid wound,
        [NotNullWhen(true)] out Entity<WoundComponent>? scarWound,
        WoundComponent? woundComponent = null)
    {
        scarWound = null;
        if (!_woundQuery.Resolve(wound, ref woundComponent))
            return false;

        if (!Random.Prob(_woundScarChance))
            return false;

        if (woundComponent.ScarWound == null || woundComponent.IsScar)
            return false;

        if (!TryCreateWound(woundComponent.HoldingWoundable,
                woundComponent.ScarWound,
                0.01f,
                out var createdWound,
                woundComponent.DamageGroup))
            return false;

        scarWound = createdWound;
        return true;
    }

    [PublicAPI]
    public override void ApplyWoundSeverity(
        EntityUid uid,
        FixedPoint2 severity,
        WoundComponent? wound = null)
    {
        if (!_woundQuery.Resolve(uid, ref wound))
            return;

        // No reason to update the wound if the woundable it is stored in is about to be deleted.
        if (TerminatingOrDeleted(wound.HoldingWoundable) || !_woundableQuery.TryComp(wound.HoldingWoundable, out var holdingComp))
            return;

        var old = wound.WoundSeverityPoint;
        wound.WoundSeverityPoint = severity > 0
            ? FixedPoint2.Clamp(old + ApplySeverityModifiers(wound.HoldingWoundable, severity, holdingComp), 0, _maxWoundSeverity)
            : FixedPoint2.Clamp(old + severity, 0, _maxWoundSeverity);

        if (wound.WoundSeverityPoint == old)
            return;

        Entity<WoundableComponent> holdingWoundable = (wound.HoldingWoundable, holdingComp);

        RaiseWoundEvents(uid, holdingWoundable, wound, old, holdingWoundable);
        CheckSeverityThresholds(uid, wound);

        UpdateWoundableIntegrity(holdingWoundable, holdingWoundable);
        CheckWoundableSeverityThresholds(holdingWoundable, holdingWoundable);
    }

    [PublicAPI]
    public override void SetWoundSeverity(
        EntityUid uid,
        FixedPoint2 severity,
        WoundComponent? wound = null)
    {
        if (!_woundQuery.Resolve(uid, ref wound))
            return;

        // No reason to update the wound if the woundable it is stored in is about to be deleted.
        if (TerminatingOrDeleted(wound.HoldingWoundable) || !_woundableQuery.TryComp(wound.HoldingWoundable, out var holdingComp))
            return;

        var old = wound.WoundSeverityPoint;
        wound.WoundSeverityPoint =
            FixedPoint2.Clamp(ApplySeverityModifiers(wound.HoldingWoundable, severity, holdingComp), 0, _maxWoundSeverity);

        if (wound.WoundSeverityPoint == old)
            return;

        Entity<WoundableComponent> holdingWoundable = (wound.HoldingWoundable, holdingComp);

        RaiseWoundEvents(uid, holdingWoundable, wound, old, holdingWoundable);
        CheckSeverityThresholds(uid, wound);

        UpdateWoundableIntegrity(holdingWoundable, holdingWoundable);
        CheckWoundableSeverityThresholds(holdingWoundable, holdingWoundable);
    }

    #endregion

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
            || woundableComponent.Wounds.Contains(wound))
            return false;

        if (!woundableComponent.AllowWounds)
            return false;

        if (woundSeverity <= WoundThresholds[WoundSeverity.Healed])
            return false;

        Xform.SetParent(wound, target);
        woundComponent.HoldingWoundable = target;
        woundComponent.DamageGroup = damageGroup;

        if (!Containers.Insert(wound, woundableComponent.Wounds))
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
        if (!_woundQuery.Resolve(woundEntity, ref wound, false)
            || !_woundableQuery.TryComp(wound.HoldingWoundable, out var woundable))
            return false;

        Log.Debug($"Wound: {MetaData(woundEntity).EntityPrototype!.ID}({woundEntity}) removed on {MetaData(wound.HoldingWoundable).EntityPrototype!.ID}({wound.HoldingWoundable})");

        UpdateWoundableIntegrity(wound.HoldingWoundable, woundable);
        CheckWoundableSeverityThresholds(wound.HoldingWoundable, woundable);

        Containers.Remove(woundEntity, woundable.Wounds!, false, true);
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
