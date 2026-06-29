using System.Linq;
using Content.Server.Backmen.Body.Systems;
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
/// Strip early gib triggers and require current total wound damage before full-body gib.
/// </summary>
public sealed partial class BkmSurgeryDestructibleSystem : EntitySystem
{
    public static readonly FixedPoint2 SurgeryGibTotalDamage = 1600;

    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private WoundSystem _wound = default!;
    [Dependency] private BkmBodySystem _body = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SurgeryTargetComponent, MapInitEvent>(OnSurgeryTargetMapInit);
        SubscribeLocalEvent<SurgeryTargetComponent, DamageChangedEvent>(OnSurgeryDamageChanged);
    }

    private void OnSurgeryTargetMapInit(Entity<SurgeryTargetComponent> ent, ref MapInitEvent args)
    {
        if (!TryComp<DestructibleComponent>(ent, out var destructible))
            return;

        PatchSurgeryGibThresholds(destructible);
    }

    private void OnSurgeryDamageChanged(Entity<SurgeryTargetComponent> ent, ref DamageChangedEvent args)
    {
        if (args.DamageDelta == null || !args.DamageIncreased)
            return;

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

            threshold.Triggered = true;
            _body.GibBody(ent);
            return;
        }
    }

    public bool ShouldFullBodyGib(EntityUid body)
    {
        return GetGibDamage(body) >= SurgeryGibTotalDamage;
    }

    /// <summary>
    /// Current damage used for full-body gib checks: max of wound severity and damageable total.
    /// </summary>
    public FixedPoint2 GetGibDamage(EntityUid body)
    {
        var damage = FixedPoint2.Zero;

        if (TryComp<BodyComponent>(body, out var bodyComp))
            damage = _wound.GetBodySeverityPoint(body, bodyComp);

        if (TryComp<DamageableComponent>(body, out var damageable))
            damage = FixedPoint2.Max(damage, _damageable.GetTotalDamage((body, damageable)));

        return damage;
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
