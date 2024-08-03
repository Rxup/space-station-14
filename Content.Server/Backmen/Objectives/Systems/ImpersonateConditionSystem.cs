using Content.Server.Backmen.Objectives.Components;
using Content.Server.Objectives.Systems;
using Content.Server.Shuttles.Systems;
using Content.Shared.Cuffs.Components;
using Content.Shared.Mind;
using Content.Shared.Objectives.Components;

namespace Content.Server.Backmen.Objectives.Systems;

/// <summary>
///     Handles escaping on the shuttle while being another person detection.
/// </summary>
public sealed class ImpersonateConditionSystem : EntitySystem
{
    [Dependency] private readonly TargetObjectiveSystem _target = default!;
    [Dependency] private readonly EmergencyShuttleSystem _emergencyShuttle = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;

    private EntityQuery<MindComponent> _mindQuery;
    private EntityQuery<MetaDataComponent> _metadataQuery;

    public override void Initialize()
    {
        base.Initialize();

        _mindQuery = GetEntityQuery<MindComponent>();
        _metadataQuery = GetEntityQuery<MetaDataComponent>();

        SubscribeLocalEvent<ImpersonateConditionComponent, ObjectiveAfterAssignEvent>(OnAfterAssign);
        SubscribeLocalEvent<ImpersonateConditionComponent, ObjectiveGetProgressEvent>(OnGetProgress);
    }

    // public override void Update(float frameTime)
    // {
    //     base.Update(frameTime);
    //
    //     var query = EntityQueryEnumerator<ImpersonateConditionComponent>();
    //     while (query.MoveNext(out var uid, out var comp))
    //     {
    //         if (comp.Name == null || comp.MindId == null)
    //             continue;
    //
    //         comp.Accumulator+= frameTime;
    //         if (comp.Accumulator < 10f)
    //             continue;
    //
    //         if (!_mindQuery.TryComp(comp.MindId, out var mind) || mind.OwnedEntity == null)
    //             continue;
    //         if (!_metadataQuery.TryComp(mind.CurrentEntity, out var metaData))
    //             continue;
    //
    //         _metaData.SetEntityName(uid, comp.Name);
    //         comp.Completed = true;
    //         // comp.Completed = metaData.EntityName == comp.Name;
    //     }
    // }

    private void OnAfterAssign(EntityUid uid, ImpersonateConditionComponent comp, ref ObjectiveAfterAssignEvent args)
    {
        if (!_target.GetTarget(uid, out var target))
            return;

        if (!TryComp<MindComponent>(target, out var targetMind) || targetMind.CharacterName == null)
            return;

        comp.Name = targetMind.CharacterName;
        comp.MindId = args.MindId;
    }

    // copypasta from escape shittle objective. eh.
    private void OnGetProgress(EntityUid uid, ImpersonateConditionComponent comp, ref ObjectiveGetProgressEvent args)
    {
        args.Progress = GetProgress(args.Mind, comp);
    }

    public float GetProgress(MindComponent mind, ImpersonateConditionComponent comp)
    {
        // not escaping alive if you're deleted/dead
        if (mind.OwnedEntity == null || _mind.IsCharacterDeadIc(mind))
            return 0f;
        // You're not escaping if you're restrained!
        if (TryComp<CuffableComponent>(mind.OwnedEntity, out var cuffed) && cuffed.CuffedHandCount > 0)
            return 0f;

        return (_emergencyShuttle.IsTargetEscaping(mind.OwnedEntity.Value) ? .5f : 0f) + (comp.Completed ? .5f : 0f);
    }
}
