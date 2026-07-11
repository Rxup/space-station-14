using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared.Backmen.Surgery.Consciousness.Components;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Random.Helpers;
using Content.Shared.Roles;
using Content.Shared.Roles.Jobs;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared.Backmen.Targeting;

public abstract partial class SharedTargetingSystem
{
    public static readonly ProtoId<CombatTargetOddsRulesPrototype> DefaultCombatTargetOddsRules = "Shooter";

    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedJobSystem _jobs = default!;
    [Dependency] private readonly SharedRoleSystem _roles = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    private EntityQuery<ConsciousnessComponent> _consciousnessQuery;
    private EntityQuery<CombatTargetOddsOverrideComponent> _overrideQuery;
    private EntityQuery<TargetingComponent> _targetingQuery;

    partial void InitializeCombatTargeting()
    {
        _consciousnessQuery = GetEntityQuery<ConsciousnessComponent>();
        _overrideQuery = GetEntityQuery<CombatTargetOddsOverrideComponent>();
        _targetingQuery = GetEntityQuery<TargetingComponent>();
    }

    public bool TryResolveCombatBodyPart(
        EntityUid victim,
        EntityUid shooter,
        EntityUid? seedEntity,
        [NotNullWhen(true)] out TargetBodyPart hitPart)
    {
        hitPart = default;

        if (!_consciousnessQuery.HasComponent(victim))
            return false;

        if (!_targetingQuery.TryComp(shooter, out var targeting))
            return false;

        var aimedPart = NormalizeTarget(targeting.Target);
        var oddsProto = ResolveCombatTargetOddsProto(shooter);

        if (!TryGetCombatTargetOddsSpread(oddsProto, aimedPart, out var weights))
            return false;

        hitPart = PickCombatBodyPart(victim, weights, seedEntity);
        return true;
    }

    public TargetBodyPart PickCombatBodyPart(
        EntityUid victim,
        Dictionary<TargetBodyPart, float> weights,
        EntityUid? seedEntity)
    {
        if (seedEntity != null)
        {
            var rand = SharedRandomExtensions.PredictedRandom(
                _timing,
                GetNetEntity(victim),
                GetNetEntity(seedEntity.Value));
            return SharedRandomExtensions.Pick(weights, rand);
        }

        return _random.Pick(weights);
    }

    public ProtoId<CombatTargetOddsPrototype> ResolveCombatTargetOddsProto(EntityUid shooter)
    {
        if (_overrideQuery.TryComp(shooter, out var oddsOverride))
            return oddsOverride.Odds;

        var rules = _proto.Index(DefaultCombatTargetOddsRules);

        if (_mind.TryGetMind(shooter, out var mindId, out _))
        {
            if (_jobs.MindTryGetJobId(mindId, out var jobId) && jobId is { } id)
            {
                if (rules.EliteJobs.Contains(id))
                    return rules.Elite;

                if (rules.SecurityJobs.Contains(id))
                    return rules.Security;
            }

            foreach (var role in _roles.MindGetAllRoleInfo(mindId))
            {
                if (!role.Antagonist || string.IsNullOrEmpty(role.Prototype))
                    continue;

                if (rules.EliteMindRoles.Contains(new EntProtoId(role.Prototype)))
                    return rules.Elite;
            }
        }

        var shooterProto = MetaData(shooter).EntityPrototype?.ID;
        if (shooterProto != null)
        {
            if (rules.EliteBorgPrototypes.Contains(shooterProto))
                return rules.Elite;

            if (rules.SecurityBorgPrototypes.Contains(shooterProto))
                return rules.Security;
        }

        return rules.Default;
    }

    public bool TryGetCombatTargetOddsSpread(
        ProtoId<CombatTargetOddsPrototype> protoId,
        TargetBodyPart aimedPart,
        [NotNullWhen(true)] out Dictionary<TargetBodyPart, float> weights)
    {
        weights = default!;

        if (!_proto.TryIndex(protoId, out var proto))
            return false;

        aimedPart = NormalizeTarget(aimedPart);

        if (!proto.Spread.TryGetValue(aimedPart, out var row) || row.Count == 0)
            return false;

        weights = new Dictionary<TargetBodyPart, float>(row);

        if (Math.Abs(proto.AimedPartWeightMultiplier - 1f) < 1e-4f)
            return true;

        if (!weights.TryGetValue(aimedPart, out var aimedWeight))
            return true;

        weights[aimedPart] = aimedWeight * proto.AimedPartWeightMultiplier;

        var sum = weights.Values.Sum();
        if (sum <= 0f)
            return false;

        foreach (var key in weights.Keys.ToList())
            weights[key] /= sum;

        return true;
    }
}
