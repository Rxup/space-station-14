using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Content.Shared.Backmen.Surgery.Wounds.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
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
        var healableWounds = ent.Comp.Wounds!.ContainedEntities.Select(Comp<WoundComponent>).Count(comp => comp.CanBeHealed);
        var healAmount = ent.Comp.HealAbility / healableWounds;

        foreach (var wound in ent.Comp.Wounds!.ContainedEntities.ToList())
        {
            var comp = Comp<WoundComponent>(wound);
            if (!comp.CanBeHealed)
                continue;

            ApplyWoundSeverity(wound, ApplyHealingRateModifiers(wound, ent.Owner, healAmount, ent.Comp), comp, true);
        }

        // That's it! o(( >ω< ))o
    }

    #region Public API

    public FixedPoint2 ApplyHealingRateModifiers(EntityUid wound, EntityUid woundable, FixedPoint2 severity, WoundableComponent? component = null)
    {
        if (!Resolve(woundable, ref component))
            return severity;

        var woundHealingMultiplier =
            _prototype.Index<DamageTypePrototype>(MetaData(wound).EntityPrototype!.ID).WoundHealingMultiplier;

        if (component.HealingMultipliers.Count == 0)
            return severity * woundHealingMultiplier;

        var toMultiply =
            component.HealingMultipliers.Sum(multiplier => (float) multiplier.Value.Change) / component.HealingMultipliers.Count;
        return severity * toMultiply * woundHealingMultiplier;
    }

    public bool TryAddHealingRateModifier(EntityUid owner, EntityUid woundable, string identifier, FixedPoint2 change, WoundableComponent? component = null)
    {
        if (!Resolve(woundable, ref component) || !_net.IsServer)
            return false;

        return component.HealingMultipliers.TryAdd(owner, new WoundableHealingMultiplier(change, identifier));
    }

    public bool TryRemoveHealingRateModifier(EntityUid owner, EntityUid woundable, WoundableComponent? component = null)
    {
        if (!Resolve(woundable, ref component)  || !_net.IsServer)
            return false;

        return component.HealingMultipliers.Remove(owner);
    }

    #endregion
}
