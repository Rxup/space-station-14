using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Damage;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Standing;
using Content.Shared.Backmen.Targeting;
using Robust.Shared.CPUJob.JobQueues;
using Robust.Shared.CPUJob.JobQueues.Queues;
using Robust.Shared.Network;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using System.Threading;
using System.Threading.Tasks;
using Content.Shared.Backmen.Surgery.Steps.Parts;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

// ReSharper disable once CheckNamespace
namespace Content.Shared.Body.Systems;

public partial class SharedBodySystem
{
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    private readonly string[] _severingDamageTypes = { "Slash", "Pierce", "Blunt" };
    private const double IntegrityJobTime = 0.005;
    private readonly JobQueue _integrityJobQueue = new(IntegrityJobTime);

    public sealed class IntegrityJob : Job<object>
    {
        private readonly SharedBodySystem _self;
        private readonly Entity<BodyPartComponent> _ent;
        public IntegrityJob(SharedBodySystem self, Entity<BodyPartComponent> ent, double maxTime, CancellationToken cancellation = default) : base(maxTime, cancellation)
        {
            _self = self;
            _ent = ent;
        }

        public IntegrityJob(SharedBodySystem self, Entity<BodyPartComponent> ent, double maxTime, IStopwatch stopwatch, CancellationToken cancellation = default) : base(maxTime, stopwatch, cancellation)
        {
            _self = self;
            _ent = ent;
        }

        protected override Task<object?> Process()
        {
            _self.ProcessIntegrityTick(_ent);

            return Task.FromResult<object?>(null);
        }
    }

    private EntityQuery<TargetingComponent> _queryTargeting;
    private void InitializeBkm()
    {
        _queryTargeting = GetEntityQuery<TargetingComponent>();
    }

    public DamageSpecifier GetHealingSpecifier(BodyPartComponent part)
    {
        var damage = new DamageSpecifier()
        {
            DamageDict = new Dictionary<string, FixedPoint2>()
            {
                { "Blunt", -part.SelfHealingAmount },
                { "Slash", -part.SelfHealingAmount },
                { "Piercing", -part.SelfHealingAmount },
                { "Heat", -part.SelfHealingAmount },
                { "Cold", -part.SelfHealingAmount },
                { "Shock", -part.SelfHealingAmount },
                { "Caustic", -part.SelfHealingAmount * 0.1}, // not much caustic healing
            }
        };

        return damage;
    }

    private void ProcessIntegrityTick(Entity<BodyPartComponent> entity)
    {
        var damage = entity.Comp.Integrity;

        if (entity.Comp is { Body: {} body}
            && damage > entity.Comp.MaxIntegrity
            && damage <= entity.Comp.IntegrityThresholds[TargetIntegrity.HeavilyWounded]
            && _queryTargeting.HasComp(body)
            && !_mobState.IsDead(body))
        {
            var healing = GetHealingSpecifier(entity);
            TryChangeIntegrity(entity, healing, false, GetTargetBodyPart(entity), out _);
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        _integrityJobQueue.Process();

        if (!_timing.IsFirstTimePredicted)
            return;

        using var query = EntityQueryEnumerator<BodyPartComponent>();
        while (query.MoveNext(out var ent, out var part))
        {
            part.HealingTimer += frameTime;

            if (part.HealingTimer >= part.HealingTime)
            {
                part.HealingTimer = 0;
                _integrityJobQueue.EnqueueJob(new IntegrityJob(this, (ent, part), IntegrityJobTime));
            }
        }
    }

    /// <summary>
    /// Propagates damage to the specified part of the entity.
    /// </summary>
    private void ApplyPartDamage(
        Entity<BodyPartComponent> partEnt,
        DamageSpecifier damage,
        BodyPartType targetType,
        TargetBodyPart targetPart,
        bool canSever,
        bool evade,
        float partMultiplier)
    {
        if (partEnt.Comp.Body is not {} body)
            return;

        _proto.TryIndex<DamageGroupPrototype>("Brute", out var proto);

        if (!TryEvadeDamage(partEnt.Comp.Body.Value, GetEvadeChance(targetType)) || evade)
        {
            TryChangeIntegrity(partEnt,
                damage * partMultiplier,
                // This is true when damage contains at least one of the brute damage types
                canSever && damage.TryGetDamageInGroup(proto!, out var dmg) && dmg > FixedPoint2.Zero,
                targetPart,
                out _);
        }
    }

    /// <summary>
    /// Adds damage to the body part and updates things about Integrity levels.
    /// </summary>
    public void TryChangeIntegrity(
        Entity<BodyPartComponent> partEnt,
        DamageSpecifier damage,
        bool canSever,
        TargetBodyPart? targetPart,
        out bool severed)
    {
        severed = false;
        if (!_timing.IsFirstTimePredicted || !_queryTargeting.HasComp(partEnt.Comp.Body))
            return;

        var partIdSlot = GetParentPartAndSlotOrNull(partEnt)?.Slot;
        var integrity = partEnt.Comp.Integrity;

        partEnt.Comp.Damage.ExclusiveAdd(damage);
        partEnt.Comp.Damage.ClampMin(partEnt.Comp.MaxIntegrity); // No over-healing!

        if (canSever
            && !HasComp<BodyPartReattachedComponent>(partEnt)
            && !partEnt.Comp.Enabled
            && integrity >= partEnt.Comp.SeverIntegrity
            && partIdSlot is not null)
            severed = true;

        CheckBodyPart(partEnt, targetPart, severed);

        if (severed && partIdSlot is not null)
            DropPart(partEnt);

        Dirty(partEnt, partEnt.Comp);
    }

    /// <summary>
    /// Sets damage to the body part and updates things about Integrity levels.
    /// Same as TryChangeIntegrity, but this can be used more fluently.
    /// </summary>
    public void TrySetIntegrity(
        Entity<BodyPartComponent> partEnt,
        DamageSpecifier damage,
        bool canSever,
        TargetBodyPart? targetPart,
        out bool severed)
    {
        severed = false;
        if (!_timing.IsFirstTimePredicted || !_queryTargeting.HasComp(partEnt.Comp.Body))
            return;

        var partIdSlot = GetParentPartAndSlotOrNull(partEnt)?.Slot;
        var integrity = partEnt.Comp.Integrity;

        partEnt.Comp.Damage = damage;
        partEnt.Comp.Damage.ClampMin(partEnt.Comp.MaxIntegrity); // No over-healing!

        if (canSever
            && !HasComp<BodyPartReattachedComponent>(partEnt)
            && !partEnt.Comp.Enabled
            && integrity >= partEnt.Comp.SeverIntegrity
            && partIdSlot is not null)
            severed = true;

        CheckBodyPart(partEnt, targetPart, severed);

        if (severed && partIdSlot is not null)
            DropPart(partEnt);

        Dirty(partEnt, partEnt.Comp);
    }

    /// <summary>
    /// This should be called after body part damage was changed.
    /// </summary>
    private void CheckBodyPart(
        Entity<BodyPartComponent> partEnt,
        TargetBodyPart? targetPart,
        bool severed)
    {
        var integrity = partEnt.Comp.Integrity;

        // KILL the body part
        if (partEnt.Comp.Enabled && integrity >= partEnt.Comp.IntegrityThresholds[TargetIntegrity.CriticallyWounded])
        {
            var ev = new BodyPartEnableChangedEvent(false);
            RaiseLocalEvent(partEnt, ref ev);
        }

        // LIVE the body part
        if (!partEnt.Comp.Enabled && integrity <= partEnt.Comp.IntegrityThresholds[TargetIntegrity.SomewhatWounded])
        {
            var ev = new BodyPartEnableChangedEvent(true);
            RaiseLocalEvent(partEnt, ref ev);
        }

        if (_queryTargeting.TryComp(partEnt.Comp.Body, out var targeting)
            && HasComp<MobStateComponent>(partEnt.Comp.Body))
        {
            var newIntegrity = GetIntegrityThreshold(partEnt.Comp, integrity, severed);
            // We need to check if the part is dead to prevent the UI from showing dead parts as alive.
            if (targetPart is not null &&
                !targetPart.Value.HasFlag(TargetBodyPart.All) &&
                targeting.BodyStatus.ContainsKey(targetPart.Value) &&
                targeting.BodyStatus[targetPart.Value] != TargetIntegrity.Dead)
            {
                targeting.BodyStatus[targetPart.Value] = newIntegrity;
                Dirty(partEnt.Comp.Body.Value, targeting);
            }

            // Revival events are handled by the server, so ends up being locked to a network event.
            if (_net.IsServer)
                RaiseNetworkEvent(new TargetIntegrityChangeEvent(GetNetEntity(partEnt.Comp.Body.Value)), partEnt.Comp.Body.Value);
        }
    }

    /// <summary>
    /// Gets the integrity of all body parts in the entity.
    /// </summary>
    public Dictionary<TargetBodyPart, TargetIntegrity> GetBodyPartStatus(EntityUid entityUid)
    {
        var result = new Dictionary<TargetBodyPart, TargetIntegrity>();

        if (!TryComp<BodyComponent>(entityUid, out var body))
            return result;

        foreach (TargetBodyPart part in Enum.GetValues(typeof(TargetBodyPart)))
        {
            result[part] = TargetIntegrity.Severed;
        }

        foreach (var partComponent in GetBodyChildren(entityUid, body))
        {
            var targetBodyPart = GetTargetBodyPart(partComponent.Component.PartType, partComponent.Component.Symmetry);

            if (targetBodyPart != null)
            {
                result[targetBodyPart.Value] = GetIntegrityThreshold(partComponent.Component, partComponent.Component.Integrity, false);
            }
        }

        return result;
    }

    public TargetBodyPart? GetTargetBodyPart(Entity<BodyPartComponent> part) => GetTargetBodyPart(part.Comp.PartType, part.Comp.Symmetry);
    public TargetBodyPart? GetTargetBodyPart(BodyPartComponent part) => GetTargetBodyPart(part.PartType, part.Symmetry);
    /// <summary>
    /// Converts Enums from BodyPartType to their Targeting system equivalent.
    /// </summary>
    public TargetBodyPart? GetTargetBodyPart(BodyPartType type, BodyPartSymmetry symmetry)
    {
        return (type, symmetry) switch
        {
            (BodyPartType.Head, _) => TargetBodyPart.Head,
            (BodyPartType.Torso, _) => TargetBodyPart.Torso,
            (BodyPartType.Arm, BodyPartSymmetry.Left) => TargetBodyPart.LeftArm,
            (BodyPartType.Arm, BodyPartSymmetry.Right) => TargetBodyPart.RightArm,
            (BodyPartType.Leg, BodyPartSymmetry.Left) => TargetBodyPart.LeftLeg,
            (BodyPartType.Leg, BodyPartSymmetry.Right) => TargetBodyPart.RightLeg,
            _ => null
        };
    }

    /// <summary>
    /// Converts Enums from Targeting system to their BodyPartType equivalent.
    /// </summary>
    public (BodyPartType Type, BodyPartSymmetry Symmetry) ConvertTargetBodyPart(TargetBodyPart targetPart)
    {
        return targetPart switch
        {
            TargetBodyPart.Head => (BodyPartType.Head, BodyPartSymmetry.None),
            TargetBodyPart.Torso => (BodyPartType.Torso, BodyPartSymmetry.None),
            TargetBodyPart.LeftArm => (BodyPartType.Arm, BodyPartSymmetry.Left),
            TargetBodyPart.RightArm => (BodyPartType.Arm, BodyPartSymmetry.Right),
            TargetBodyPart.LeftLeg => (BodyPartType.Leg, BodyPartSymmetry.Left),
            TargetBodyPart.RightLeg => (BodyPartType.Leg, BodyPartSymmetry.Right),
            _ => (BodyPartType.Torso, BodyPartSymmetry.None)
        };

    }

    /// <summary>
    /// Fetches the damage multiplier for part integrity based on part types.
    /// </summary>
    public static float GetPartDamageModifier(BodyPartType partType)
    {
        return partType switch
        {
            BodyPartType.Head => 0.5f, // 50% damage, necks are hard to cut
            BodyPartType.Torso => 1.0f, // 100% damage
            BodyPartType.Arm => 0.7f, // 70% damage
            BodyPartType.Leg => 0.7f, // 70% damage
            _ => 0.5f
        };
    }

    /// <summary>
    /// Fetches the TargetIntegrity equivalent of the current integrity value for the body part.
    /// </summary>
    public static TargetIntegrity GetIntegrityThreshold(BodyPartComponent component, float integrity, bool severed)
    {
        var enabled = component.Enabled;

        if (severed)
            return TargetIntegrity.Severed;

        if (!enabled)
            return TargetIntegrity.Disabled;

        // Go through every Integrity threshold and pick
        var targetIntegrity = TargetIntegrity.Healthy;
        foreach (var threshold in component.IntegrityThresholds)
        {
            if (integrity <= threshold.Value)
                targetIntegrity = threshold.Key;
        }

        return targetIntegrity;
    }

    /// <summary>
    /// Fetches the chance to evade integrity damage for a body part.
    /// Used when the entity is not dead, laying down, or incapacitated.
    /// </summary>
    public static float GetEvadeChance(BodyPartType partType)
    {
        return partType switch
        {
            BodyPartType.Head => 0.70f,  // 70% chance to evade
            BodyPartType.Arm => 0.20f,   // 20% chance to evade
            BodyPartType.Leg => 0.20f,   // 20% chance to evade
            BodyPartType.Torso => 0f, // 0% chance to evade
            _ => 0f
        };
    }



    public bool CanEvadeDamage(Entity<MobStateComponent?> uid)
    {
        if (!Resolve(uid, ref uid.Comp))
            return false;

        return TryComp<StandingStateComponent>(uid, out var standingState)
               && !_mobState.IsCritical(uid, uid)
               && !_mobState.IsDead(uid, uid)
               && standingState.CurrentState != StandingState.Lying;
    }

    public bool TryEvadeDamage(Entity<MobStateComponent?> uid, float evadeChance)
    {
        if (!Resolve(uid, ref uid.Comp))
            return false;

        if (!CanEvadeDamage(uid))
            return false;

        return _random.NextFloat() < evadeChance;
    }
}
