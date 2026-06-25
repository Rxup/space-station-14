using System.Linq;
using Content.Server.Destructible;
using Content.Server.Destructible.Thresholds;
using Content.Server.Destructible.Thresholds.Behaviors;
using Content.Shared.Backmen.Surgery;
using Content.Shared.Backmen.Surgery.Wounds.Systems;
using Content.Shared.Body;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Destructible.Thresholds.Triggers;
using Content.Shared.FixedPoint;

namespace Content.Server.Backmen.Body;

/// <summary>
/// Surgery patients inherit MobDamageable's blunt gib threshold (400) via prototype merge.
/// Strip early gib triggers and require total wound damage before full-body gib.
/// </summary>
public sealed class BkmSurgeryDestructibleSystem : EntitySystem
{
    public static readonly FixedPoint2 SurgeryGibTotalDamage = 1000;

    [Dependency] private readonly DestructibleSystem _destructible = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly WoundSystem _wound = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SurgeryTargetComponent, MapInitEvent>(OnSurgeryTargetMapInit);
        SubscribeLocalEvent<SurgeryTargetComponent, DamageChangedEvent>(OnSurgeryDamageChanged);
    }

    private void OnSurgeryTargetMapInit(Entity<SurgeryTargetComponent> ent, ref MapInitEvent args)
    {
        EnsureComp<BkmSurgeryGibTrackerComponent>(ent);

        if (!TryComp<DestructibleComponent>(ent, out var destructible))
            return;

        PatchSurgeryGibThresholds(destructible);
    }

    private void OnSurgeryDamageChanged(Entity<SurgeryTargetComponent> ent, ref DamageChangedEvent args)
    {
        if (args.DamageDelta == null || !args.DamageIncreased)
            return;

        if (TryComp<BkmSurgeryGibTrackerComponent>(ent, out var tracker))
            tracker.AccumulatedDamage += args.DamageDelta.GetTotal();

        if (!ShouldFullBodyGib(ent))
            return;

        if (!TryComp<DestructibleComponent>(ent, out var destructible))
            return;

        foreach (var threshold in destructible.Thresholds)
        {
            if (!threshold.Behaviors.Any(behavior => behavior is GibBehavior))
                continue;

            if (threshold.Triggered)
                return;

            _destructible.Execute(threshold, ent, args.Origin);
            return;
        }
    }

    public bool ShouldFullBodyGib(EntityUid body)
    {
        return GetAccumulatedGibDamage(body) >= SurgeryGibTotalDamage;
    }

    public FixedPoint2 GetAccumulatedGibDamage(EntityUid body)
    {
        var woundSeverity = TryComp<BodyComponent>(body, out var bodyComp)
            ? _wound.GetBodySeverityPoint(body, bodyComp)
            : FixedPoint2.Zero;

        var accumulated = FixedPoint2.Zero;

        if (TryComp<BkmSurgeryGibTrackerComponent>(body, out var tracker))
            accumulated = tracker.AccumulatedDamage;

        if (TryComp<DamageableComponent>(body, out var damageable))
            accumulated = FixedPoint2.Max(accumulated, _damageable.GetTotalDamage((body, damageable)));

        return FixedPoint2.Max(accumulated, woundSeverity);
    }

    private static void PatchSurgeryGibThresholds(DestructibleComponent comp)
    {
        comp.Thresholds.RemoveAll(threshold =>
            threshold.Behaviors.Any(behavior => behavior is GibBehavior));

        comp.Thresholds.Add(new DamageThreshold
        {
            Trigger = new DamageTrigger { Damage = SurgeryGibTotalDamage },
            Behaviors = { new GibBehavior() },
            TriggersOnce = true,
        });
    }
}
