using Content.Shared.Backmen.Targeting;
using Content.Shared.Implants;
using Content.Shared.Mind;
using Content.Shared.Roles.Jobs;
using Robust.Shared.Prototypes;

namespace Content.Server.Backmen.Targeting;

/// <summary>
/// Applies combat spread tier overrides from combat training implants.
/// </summary>
public sealed partial class CombatTrainingImplantSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private SharedJobSystem _jobs = default!;
    [Dependency] private SharedMindSystem _mind = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CombatTrainingImplantComponent, ImplantImplantedEvent>(OnImplantImplanted);
        SubscribeLocalEvent<CombatTrainingImplantComponent, ImplantRemovedEvent>(OnImplantRemoved);
    }

    private void OnImplantImplanted(Entity<CombatTrainingImplantComponent> ent, ref ImplantImplantedEvent ev)
    {
        var oddsOverride = EnsureComp<CombatTargetOddsOverrideComponent>(ev.Implanted);
        oddsOverride.Odds = ent.Comp.Odds;
        oddsOverride.FromImplant = true;
        Dirty(ev.Implanted, oddsOverride);
    }

    private void OnImplantRemoved(Entity<CombatTrainingImplantComponent> ent, ref ImplantRemovedEvent ev)
    {
        if (!TryComp<CombatTargetOddsOverrideComponent>(ev.Implanted, out var oddsOverride) || !oddsOverride.FromImplant)
            return;

        if (_mind.TryGetMind(ev.Implanted, out var mindId, out _)
            && _jobs.MindTryGetJobId(mindId, out var jobId)
            && jobId is { } id
            && _proto.TryIndex(SharedTargetingSystem.DefaultCombatTargetOddsRules, out var rules)
            && rules.SecurityJobs.Contains(id))
            return;

        RemComp<CombatTargetOddsOverrideComponent>(ev.Implanted);
    }
}
