using Content.Server.Backmen.Objectives;
using Content.Server.Objectives.Components;
using Content.Shared.Mind.Filters;
using Content.Shared.Objectives.Components;
using Content.Shared.Objectives.Systems;

namespace Content.Server.Objectives.Systems;

/// <summary>
/// Handles assinging a target to an objective entity with <see cref="TargetObjectiveComponent"/> using different components.
/// These can be combined with condition components for objective completions in order to create a variety of objectives.
/// </summary>
public sealed partial class PickObjectiveTargetSystem : EntitySystem
{
    [Dependency] private TargetObjectiveSystem _objective = default!;
    [Dependency] private TargetSystem _target = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PickSpecificPersonComponent, ObjectiveAssignedEvent>(OnSpecificPersonAssigned);
        SubscribeLocalEvent<PickRandomPersonComponent, ObjectiveAssignedEvent>(OnRandomPersonAssigned);
    }

    private void OnSpecificPersonAssigned(Entity<PickSpecificPersonComponent> ent, ref ObjectiveAssignedEvent args)
    {
        // invalid objective prototype
        if (!TryComp<TargetObjectiveComponent>(ent.Owner, out var target))
        {
            args.Cancelled = true;
            return;
        }

        // target already assigned
        if (target.Target != null)
            return;

        if (args.Mind.OwnedEntity == null)
        {
            args.Cancelled = true;
            return;
        }

        var user = args.Mind.OwnedEntity.Value;
        if (!TryComp<TargetOverrideComponent>(user, out var targetComp) || targetComp.Target == null)
        {
            args.Cancelled = true;
            return;
        }

        _objective.SetTarget(ent.Owner, targetComp.Target.Value);
    }

    private void OnRandomPersonAssigned(Entity<PickRandomPersonComponent> ent, ref ObjectiveAssignedEvent args)
    {
        // invalid objective prototype
        if (!TryComp<TargetObjectiveComponent>(ent, out var target))
        {
            args.Cancelled = true;
            return;
        }

        // target already assigned
        if (target.Target != null)
            return;

        // couldn't find a target :(
        var filters = new List<MindFilter>(ent.Comp.Filters) { new CentComExcludeMindFilter() }; // backmen: centcom
        if (_target.PickFromPool(ent.Comp.Pool, filters, args.MindId) is not {} picked)
        {
            args.Cancelled = true;
            return;
        }

        _objective.SetTarget(ent, picked, target);
    }
}
