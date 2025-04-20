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
using Robust.Shared.Threading;

namespace Content.Server.Backmen.Surgery.Wounds.Systems;

public sealed class ServerWoundSystem : WoundSystem
{
    private record struct IntegrityJob : IParallelRobustJob
    {
        public WoundSystem System { get; init; }
        public Entity<WoundableComponent> Owner { get; init; }
        public List<Entity<WoundComponent>> WoundsToHeal { get; init; }
        public FixedPoint2 HealAmount { get; init; }
        public void Execute(int index)
        {
            System.ApplyWoundSeverity(WoundsToHeal[index],
                System.ApplyHealingRateMultipliers(WoundsToHeal[index], Owner, HealAmount, Owner));
        }
    }


    [Dependency] private readonly IParallelManager _parallel = default!;

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

    private void UpdateWoundableIntegrity(EntityUid uid, WoundableComponent? component = null)
    {
        if (!_woundableQuery.Resolve(uid, ref component, false) || component.Wounds == null)
            return;

        // Ignore scars for woundable integrity.. Unless you want to confuse people with minor woundable state
        var damage =
            component.Wounds.ContainedEntities.Select(_woundQuery.Comp)
                .Where(wound => !wound.IsScar)
                .Aggregate(FixedPoint2.Zero, (current, wound) => current + wound.WoundIntegrityDamage);

        var newIntegrity = FixedPoint2.Clamp(component.IntegrityCap - damage, 0, component.IntegrityCap);
        if (newIntegrity == component.WoundableIntegrity)
            return;

        var ev = new WoundableIntegrityChangedEvent(component.WoundableIntegrity, newIntegrity);
        RaiseLocalEvent(uid, ref ev);

        var bodySeverity = FixedPoint2.Zero;
        var bodyPart = Comp<BodyPartComponent>(uid);

        if (bodyPart.Body.HasValue)
        {
            var rootPart = Comp<BodyComponent>(bodyPart.Body.Value).RootContainer.ContainedEntity;
            if (rootPart.HasValue)
            {
                bodySeverity =
                    GetAllWoundableChildren(rootPart.Value)
                        .Aggregate(bodySeverity, (current, woundable) => current + GetWoundableIntegrityDamage(woundable, woundable));
            }

            var ev1 = new WoundableIntegrityChangedOnBodyEvent(
                (uid, component),
                bodySeverity - (component.WoundableIntegrity - newIntegrity),
                bodySeverity);
            RaiseLocalEvent(bodyPart.Body.Value, ref ev1);
        }

        component.WoundableIntegrity = newIntegrity;
        Dirty(uid, component);
    }

    private void CheckWoundableSeverityThresholds(EntityUid woundable, WoundableComponent? component = null)
    {
        if (!_woundableQuery.Resolve(woundable, ref component, false))
            return;

        var nearestSeverity = component.WoundableSeverity;
        foreach (var (severity, value) in component.Thresholds.OrderByDescending(kv => kv.Value))
        {
            if (component.WoundableIntegrity >= component.IntegrityCap)
            {
                nearestSeverity = WoundableSeverity.Healthy;
                break;
            }

            if (component.WoundableIntegrity < value)
                continue;

            nearestSeverity = severity;
            break;
        }

        if (nearestSeverity != component.WoundableSeverity)
        {
            var ev = new WoundableSeverityChangedEvent(component.WoundableSeverity, nearestSeverity);
            RaiseLocalEvent(woundable, ref ev);
        }
        component.WoundableSeverity = nearestSeverity;

        Dirty(woundable, component);

        var bodyPart = Comp<BodyPartComponent>(woundable);
        if (bodyPart.Body == null)
            return;

        if (!TryComp<TargetingComponent>(bodyPart.Body.Value, out var targeting))
            return;

        targeting.BodyStatus = GetWoundableStatesOnBodyPainFeels(bodyPart.Body.Value);
        Dirty(bodyPart.Body.Value, targeting);

        RaiseNetworkEvent(new TargetIntegrityChangeEvent(GetNetEntity(bodyPart.Body.Value)), bodyPart.Body.Value);
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

            _parallel.ProcessNow(new IntegrityJob
                {
                    System = this,
                    Owner = owner,
                    WoundsToHeal = woundsToHeal,
                    HealAmount = healAmount,
                },
                woundsToHeal.Count);
        }
    }
}
