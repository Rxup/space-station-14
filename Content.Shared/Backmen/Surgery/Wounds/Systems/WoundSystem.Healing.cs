using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Content.Shared.Backmen.Surgery.Traumas.Components;
using Content.Shared.Backmen.Surgery.Wounds.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using JetBrains.Annotations;
using Robust.Shared.CPUJob.JobQueues;
using Robust.Shared.CPUJob.JobQueues.Queues;
using Robust.Shared.Timing;

namespace Content.Shared.Backmen.Surgery.Wounds.Systems;

public partial class WoundSystem
{
    private const double WoundableJobTime = 0.005;
    private readonly JobQueue _woundableJobQueue = new(WoundableJobTime);
    public sealed class IntegrityJob : Job<object>
    {
        private readonly WoundSystem _self;
        private readonly Entity<WoundableComponent> _ent;
        public IntegrityJob(WoundSystem self, Entity<WoundableComponent> ent, double maxTime, CancellationToken cancellation = default) : base(maxTime, cancellation)
        {
            _self = self;
            _ent = ent;
        }

        public IntegrityJob(WoundSystem self, Entity<WoundableComponent> ent, double maxTime, IStopwatch stopwatch, CancellationToken cancellation = default) : base(maxTime, stopwatch, cancellation)
        {
            _self = self;
            _ent = ent;
        }

        protected override Task<object?> Process()
        {
            _self.ProcessHealing(_ent);
            return Task.FromResult<object?>(null);
        }
    }

    private void ProcessHealing(Entity<WoundableComponent> ent)
    {
        if (!_timing.IsFirstTimePredicted)
            return;

        var healableWounds = ent.Comp.Wounds.ContainedEntities.Count(wound => CanHealWound(wound));
        var healAmount = -ent.Comp.HealAbility / healableWounds;

        foreach (var wound in ent.Comp.Wounds.ContainedEntities.ToList().Where(wound => CanHealWound(wound)))
        {
            ApplyWoundSeverity(wound, ApplyHealingRateMultipliers(wound, ent.Owner, healAmount, ent.Comp));
        }

        // That's it! o(( >ω< ))o
    }

    #region Public API

    [PublicAPI]
    public bool TryHaltAllBleeding(EntityUid woundable, WoundableComponent? component = null, bool force = false)
    {
        if (!Resolve(woundable, ref component) || component.Wounds.Count == 0)
            return true;

        foreach (var wound in GetWoundableWounds(woundable, component))
        {
            if (force)
            {
                // For wounds like scars. Temporary for now
                wound.Comp.CanBeHealed = true;
            }

            if (!TryComp<BleedInflicterComponent>(wound, out var bleeds))
                continue;

            bleeds.IsBleeding = false;
        }

        return true;
    }

    [PublicAPI]
    public void ForceHealWoundsOnWoundable(EntityUid woundable,
        out FixedPoint2 healed,
        DamageGroupPrototype? damageGroup = null,
        WoundableComponent? component = null)
    {
        healed = 0;
        if (!Resolve(woundable, ref component))
            return;

        var woundsToHeal =
            GetWoundableWounds(woundable, component)
                .Where(wound => damageGroup == null || wound.Comp.DamageGroup == damageGroup)
                .ToList();

        foreach (var wound in woundsToHeal)
        {
            healed += wound.Comp.WoundSeverityPoint;
            RemoveWound(wound, wound);
        }

        UpdateWoundableIntegrity(woundable, component);
        CheckWoundableSeverityThresholds(woundable, component);
    }

    [PublicAPI]
    public bool TryHealWoundsOnWoundable(EntityUid woundable,
        FixedPoint2 healAmount,
        out FixedPoint2 healed,
        WoundableComponent? component = null,
        DamageGroupPrototype? damageGroup = null,
        bool ignoreMultipliers = false)
    {
        healed = 0;
        if (!Resolve(woundable, ref component))
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
                ? ApplyHealingRateMultipliers(wound, woundable, -healNumba, component)
                : -healNumba;

            actualHeal += -heal;
            ApplyWoundSeverity(wound, heal, wound);
        }

        UpdateWoundableIntegrity(woundable, component);
        CheckWoundableSeverityThresholds(woundable, component);

        healed = actualHeal;
        return actualHeal > 0;
    }

    [PublicAPI]
    public bool TryHealWoundsOnWoundable(EntityUid woundable,
        FixedPoint2 healAmount,
        string damageType,
        out FixedPoint2 healed,
        WoundableComponent? component = null,
        bool ignoreMultipliers = false)
    {
        healed = 0;
        if (!Resolve(woundable, ref component))
            return false;

        var woundsToHeal =
            (from wound in component.Wounds.ContainedEntities
                let woundComp = Comp<WoundComponent>(wound)
                where CanHealWound(wound)
                where damageType == MetaData(wound).EntityPrototype!.ID
                select (wound, woundComp)).Select(dummy => (Entity<WoundComponent>) dummy)
            .ToList();

        if (woundsToHeal.Count == 0)
            return false;

        var healNumba = healAmount / woundsToHeal.Count;
        var actualHeal = (FixedPoint2) 0;
        foreach (var wound in woundsToHeal)
        {
            var heal = ignoreMultipliers
                ? ApplyHealingRateMultipliers(wound, woundable, -healNumba, component)
                : -healNumba;

            actualHeal += -heal;
            ApplyWoundSeverity(wound, heal, wound);
        }

        UpdateWoundableIntegrity(woundable, component);
        CheckWoundableSeverityThresholds(woundable, component);

        healed = actualHeal;
        return actualHeal > 0;
    }

    [PublicAPI]
    public bool TryGetWoundableWithMostDamage(
        EntityUid body,
        [NotNullWhen(true)] out Entity<WoundableComponent>? woundable,
        string? damageGroup = null,
        bool healable = false)
    {
        var biggestDamage = (FixedPoint2) 0;

        woundable = null;
        foreach (var bodyPart in _body.GetBodyChildren(body))
        {
            if (!TryComp<WoundableComponent>(bodyPart.Id, out var woundableComp))
                continue;

            var woundableDamage = GetWoundableSeverityPoint(bodyPart.Id, woundableComp, damageGroup, healable);
            if (woundableDamage <= biggestDamage)
                continue;

            biggestDamage = woundableDamage;
            woundable = (bodyPart.Id, woundableComp);
        }

        return woundable != null;
    }

    [PublicAPI]
    public bool HasDamageOfType(
        EntityUid woundable,
        string damageType,
        bool healable = false)
    {
        if (healable)
        {
            return GetWoundableWounds(woundable)
                .Where(wound => CanHealWound(wound, wound))
                .Any(wound => MetaData(wound).EntityPrototype!.ID == damageType);
        }

        return GetWoundableWounds(woundable).Any(wound => MetaData(wound).EntityPrototype!.ID == damageType);
    }

    [PublicAPI]
    public bool HasDamageOfGroup(
        EntityUid woundable,
        string damageGroup,
        bool healable = false)
    {
        if (healable)
        {
            return GetWoundableWounds(woundable)
                .Where(wound => CanHealWound(wound, wound))
                .Any(wound => wound.Comp.DamageGroup?.ID == damageGroup);
        }

        return GetWoundableWounds(woundable).Any(wound => wound.Comp.DamageGroup?.ID == damageGroup);
    }

    [PublicAPI]
    public FixedPoint2 ApplyHealingRateMultipliers(EntityUid wound, EntityUid woundable, FixedPoint2 severity, WoundableComponent? component = null)
    {
        if (!Resolve(woundable, ref component))
            return severity;

        if (!Comp<WoundComponent>(wound).CanBeHealed)
            return FixedPoint2.Zero;

        var woundHealingMultiplier =
            _prototype.Index<DamageTypePrototype>(MetaData(wound).EntityPrototype!.ID).WoundHealingMultiplier;

        if (component.HealingMultipliers.Count == 0)
            return severity * woundHealingMultiplier;

        var toMultiply =
            component.HealingMultipliers.Sum(multiplier => (float) multiplier.Value.Change) / component.HealingMultipliers.Count;
        return severity * toMultiply * woundHealingMultiplier;
    }

    [PublicAPI]
    public bool TryAddHealingRateMultiplier(EntityUid owner, EntityUid woundable, string identifier, FixedPoint2 change, WoundableComponent? component = null)
    {
        if (!Resolve(woundable, ref component) || !_net.IsServer)
            return false;

        return component.HealingMultipliers.TryAdd(owner, new WoundableHealingMultiplier(change, identifier));
    }

    [PublicAPI]
    public bool TryRemoveHealingRateMultiplier(EntityUid owner, EntityUid woundable, WoundableComponent? component = null)
    {
        if (!Resolve(woundable, ref component)  || !_net.IsServer)
            return false;

        return component.HealingMultipliers.Remove(owner);
    }

    [PublicAPI]
    public bool CanHealWound(EntityUid wound, WoundComponent? comp = null)
    {
        if (!Resolve(wound, ref comp))
            return false;

        if (!comp.CanBeHealed)
            return false;

        var holdingWoundable = comp.HoldingWoundable;

        var ev = new WoundHealAttemptOnWoundableEvent((wound, comp));
        RaiseLocalEvent(holdingWoundable, ref ev);

        if (ev.Cancelled)
            return false;

        var ev1 = new WoundHealAttemptEvent((holdingWoundable, Comp<WoundableComponent>(holdingWoundable)));
        RaiseLocalEvent(wound, ref ev1);

        return !ev1.Cancelled;
    }

    #endregion
}
