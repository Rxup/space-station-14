using System.Linq;
using Content.Server.Objectives.Components;
using Content.Server.Shuttles.Systems;
using Content.Server.Station.Components;
using Content.Shared.CCVar;
using Content.Shared.Mind;
using Content.Shared.Objectives.Components;
using Content.Shared.Roles;
using Content.Shared.Roles.Jobs;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using System.Linq;
using Content.Server.Revolutionary.Components;

namespace Content.Server.Objectives.Systems;

/// <summary>
/// Handles kill person condition logic and picking random kill targets.
/// </summary>
public sealed class KillPersonConditionSystem : EntitySystem
{
    [Dependency] private readonly EmergencyShuttleSystem _emergencyShuttle = default!;
    [Dependency] private readonly IConfigurationManager _config = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly TargetObjectiveSystem _target = default!;
    [Dependency] private readonly SharedRoleSystem _roleSystem = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    private static readonly ProtoId<DepartmentPrototype> _ccDep = "CentCom";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<KillPersonConditionComponent, ObjectiveGetProgressEvent>(OnGetProgress);
    }

    private void OnGetProgress(EntityUid uid, KillPersonConditionComponent comp, ref ObjectiveGetProgressEvent args)
    {
        if (!_target.GetTarget(uid, out var target))
            return;

        args.Progress = GetProgress(target.Value, comp.RequireDead, comp.RequireMaroon);
    }

    private void OnPersonAssigned(EntityUid uid, PickRandomPersonComponent comp, ref ObjectiveAssignedEvent args)
    {
        // invalid objective prototype
        if (!TryComp<TargetObjectiveComponent>(uid, out var target))
        {
            args.Cancelled = true;
            return;
        }

        // target already assigned
        if (target.Target != null)
            return;

        var allHumans = _mind.GetAliveHumans(args.MindId);

        // Can't have multiple objectives to kill the same person
        foreach (var objective in args.Mind.Objectives)
        {
            if (HasComp<KillPersonConditionComponent>(objective) && TryComp<TargetObjectiveComponent>(objective, out var kill))
            {
                allHumans.RemoveWhere(x => x.Owner == kill.Target);
            }
        }

        // no other humans to kill
        if (allHumans.Count == 0)
        {
            args.Cancelled = true;
            return;
        }

        // start-backmen: centcom
        FilterCentCom(allHumans);

        if (allHumans.Count == 0)
        {
            args.Cancelled = true;
            return;
        }
        // end-backmen: centcom

        _target.SetTarget(uid, _random.Pick(allHumans), target);

    }

    // start-backmen: centcom
    private void FilterCentCom(HashSet<Entity<MindComponent>> minds)
    {
        var centcom = _prototype.Index(_ccDep);
        foreach (var mindId in minds.ToArray())
        {
            if (!_roleSystem.MindHasRole<JobRoleComponent>(mindId.Owner, out var job) || job.Value.Comp1.JobPrototype == null)
            {
                continue;
            }

            if (!centcom.Roles.Contains(job.Value.Comp1.JobPrototype.Value))
            {
                continue;
            }

            minds.Remove(mindId);
        }
    }
    // end-backmen: centcom

    private void OnHeadAssigned(EntityUid uid, PickRandomHeadComponent comp, ref ObjectiveAssignedEvent args)
    {
        // invalid prototype
        if (!TryComp<TargetObjectiveComponent>(uid, out var target))
        {
            args.Cancelled = true;
            return;
        }

        // target already assigned
        if (target.Target != null)
            return;

        // no other humans to kill
        var allHumans = _mind.GetAliveHumans(args.MindId);
        if (allHumans.Count == 0)
        {
            args.Cancelled = true;
            return;
        }

        // start-backmen: centcom
        FilterCentCom(allHumans);

        if (allHumans.Count == 0)
        {
            args.Cancelled = true;
            return;
        }
        // end-backmen: centcom

        var allHeads = new HashSet<Entity<MindComponent>>();
        foreach (var person in allHumans)
        {
            if (TryComp<MindComponent>(person, out var mind) && mind.OwnedEntity is { } ent && HasComp<CommandStaffComponent>(ent))
                allHeads.Add(person);
        }

        if (allHeads.Count == 0)
            allHeads = allHumans; // fallback to non-head target

        _target.SetTarget(uid, _random.Pick(allHeads), target);
    }

    private float GetProgress(EntityUid target, bool requireDead, bool requireMaroon)
    {
        // deleted or gibbed or something, counts as dead
        if (!TryComp<MindComponent>(target, out var mind) || mind.OwnedEntity == null)
            return 1f;

        var targetDead = _mind.IsCharacterDeadIc(mind);
        var targetMarooned = !_emergencyShuttle.IsTargetEscaping(mind.OwnedEntity.Value) || _mind.IsCharacterUnrevivableIc(mind);
        if (!_config.GetCVar(CCVars.EmergencyShuttleEnabled) && requireMaroon)
        {
            requireDead = true;
            requireMaroon = false;
        }

        if (requireDead && !targetDead)
            return 0f;

        // Always failed if the target needs to be marooned and the shuttle hasn't even arrived yet
        if (requireMaroon && !_emergencyShuttle.EmergencyShuttleArrived)
            return 0f;

        // If the shuttle hasn't left, give 50% progress if the target isn't on the shuttle as a "almost there!"
        if (requireMaroon && !_emergencyShuttle.ShuttlesLeft)
            return targetMarooned ? 0.5f : 0f;

        // If the shuttle has already left, and the target isn't on it, 100%
        if (requireMaroon && _emergencyShuttle.ShuttlesLeft)
            return targetMarooned ? 1f : 0f;

        return 1f; // Good job you did it woohoo
    }
}
