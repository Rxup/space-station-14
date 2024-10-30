using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Damage;
using Content.Shared.Mobs.Systems;
using Content.Shared.Mobs.Components;
using Content.Shared.Standing;
using Content.Shared.Targeting;
using Content.Shared.Targeting.Events;
using Robust.Shared.Network;
using Robust.Shared.Random;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Content.Shared.Backmen.Surgery.Steps.Parts;
using Robust.Shared.CPUJob.JobQueues;
using Robust.Shared.CPUJob.JobQueues.Queues;
using Robust.Shared.Timing;

namespace Content.Shared.Body.Systems;

public partial class SharedBodySystem
{
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
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

    private void ProcessIntegrityTick(Entity<BodyPartComponent> entity)
    {
        if (entity.Comp is { Body: {} body, Integrity: > BodyPartComponent.MaxIntegrity/2 and < BodyPartComponent.MaxIntegrity }
            && _queryTargeting.HasComp(body)
            && !_mobState.IsDead(body))
        {
            var healing = entity.Comp.SelfHealingAmount;
            if (healing + entity.Comp.Integrity > BodyPartComponent.MaxIntegrity)
                healing = entity.Comp.Integrity - BodyPartComponent.MaxIntegrity;

            TryChangeIntegrity(entity,
                healing,
                false,
                GetTargetBodyPart(entity),
                out _);
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
    /// Propagates damage to the specified parts of the entity.
    /// </summary>
    private void ApplyPartDamage(
        Entity<BodyPartComponent> partEnt,
        DamageSpecifier damage,
        BodyPartType targetType,
        TargetBodyPart targetPart,
        bool canSever,
        float partMultiplier)
    {
        if (
            partEnt.Comp.Body is not {} body ||
            !TryComp<MobStateComponent>(body, out var mobState))
            return;

        foreach (var (damageType, damageValue) in damage.DamageDict)
        {
            if (damageValue.Float() == 0
                || TryEvadeDamage((body, mobState), GetEvadeChance(targetType)))
                continue;

            var modifier = GetDamageModifier(damageType);
            var partModifier = GetPartDamageModifier(targetType);
            var integrityDamage = damageValue.Float() * modifier * partModifier * partMultiplier;
            TryChangeIntegrity(partEnt,
                integrityDamage,
                canSever && _severingDamageTypes.Contains(damageType),
                targetPart,
                out var severed);

            if (severed)
                break;
        }
    }

    public void TryChangeIntegrity(Entity<BodyPartComponent> partEnt,
        float integrity,
        bool canSever,
        TargetBodyPart? targetPart,
        out bool severed)
    {
        severed = false;
        if (!_timing.IsFirstTimePredicted || !_queryTargeting.HasComp(partEnt.Comp.Body))
            return;

        var partIdSlot = GetParentPartAndSlotOrNull(partEnt)?.Slot;
        var originalIntegrity = partEnt.Comp.Integrity;
        partEnt.Comp.Integrity = Math.Min(BodyPartComponent.MaxIntegrity, partEnt.Comp.Integrity - integrity);
        if (canSever
            && !HasComp<BodyPartReattachedComponent>(partEnt)
            && !partEnt.Comp.Enabled
            && partEnt.Comp.Integrity <= 0
            && partIdSlot is not null)
            severed = true;

        // This will also prevent the torso from being removed.
        if (partEnt.Comp.Enabled
            && partEnt.Comp.Integrity <= BodyPartComponent.CritIntegrity)
        {
            var ev = new BodyPartEnableChangedEvent(false);
            RaiseLocalEvent(partEnt, ref ev);
        }
        else if (!partEnt.Comp.Enabled
            && partEnt.Comp.Integrity >= BodyPartComponent.LightIntegrity)
        {
            var ev = new BodyPartEnableChangedEvent(true);
            RaiseLocalEvent(partEnt, ref ev);
        }

        if (Math.Abs(partEnt.Comp.Integrity - originalIntegrity) > 0.01
            && _queryTargeting.TryComp(partEnt.Comp.Body, out var targeting)
            && HasComp<MobStateComponent>(partEnt.Comp.Body))
        {
            var newIntegrity = GetIntegrityThreshold(partEnt.Comp.Integrity, severed, partEnt.Comp.Enabled);
            // We need to check if the part is dead to prevent the UI from showing dead parts as alive.
            if (targetPart is not null && targeting.BodyStatus[targetPart.Value] != TargetIntegrity.Dead)
            {
                targeting.BodyStatus[targetPart.Value] = newIntegrity;
                Dirty(partEnt.Comp.Body.Value, targeting);
            }

            // Revival events are handled by the server, so ends up being locked to a network event.
            if (_net.IsServer)
                RaiseNetworkEvent(new TargetIntegrityChangeEvent(GetNetEntity(partEnt.Comp.Body.Value)), partEnt.Comp.Body.Value);
        }

        if (severed && partIdSlot is not null)
            DropPart(partEnt);

        Dirty(partEnt, partEnt.Comp);
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
                result[targetBodyPart.Value] = GetIntegrityThreshold(partComponent.Component.Integrity, false, partComponent.Component.Enabled);
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
    /// Fetches the damage multiplier for part integrity based on damage types.
    /// </summary>
    public float GetDamageModifier(string damageType)
    {
        return damageType switch
        {
            "Blunt" => 0.8f,
            "Slash" => 1.2f,
            "Pierce" => 0.5f,
            "Heat" => 1.0f,
            "Cold" => 1.0f,
            "Shock" => 0.8f,
            "Poison" => 0.8f,
            "Radiation" => 0.8f,
            "Cellular" => 0.8f,
            _ => 0.5f
        };
    }

    /// <summary>
    /// Fetches the damage multiplier for part integrity based on part types.
    /// </summary>
    public float GetPartDamageModifier(BodyPartType partType)
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
    public TargetIntegrity GetIntegrityThreshold(float integrity, bool severed, bool enabled)
    {
        if (severed)
            return TargetIntegrity.Severed;
        else if (!enabled)
            return TargetIntegrity.Disabled;
        else
            return integrity switch
            {
                <= BodyPartComponent.CritIntegrity => TargetIntegrity.CriticallyWounded,
                <= BodyPartComponent.HeavyIntegrity => TargetIntegrity.HeavilyWounded,
                <= BodyPartComponent.MedIntegrity => TargetIntegrity.ModeratelyWounded,
                <= BodyPartComponent.SomewhatIntegrity => TargetIntegrity.SomewhatWounded,
                <= BodyPartComponent.LightIntegrity => TargetIntegrity.LightlyWounded,
                _ => TargetIntegrity.Healthy
            };
    }

    /// <summary>
    /// Fetches the chance to evade integrity damage for a body part.
    /// Used when the entity is not dead, laying down, or incapacitated.
    /// </summary>
    public float GetEvadeChance(BodyPartType partType)
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
