using Content.Server.Backmen.Psionics.Objectives.Components;
using Content.Shared.Backmen.Soul;
using Content.Shared.Mind;
using Content.Shared.Objectives.Components;

namespace Content.Server.Backmen.Psionics.Objectives;

public sealed class BecomeGolemConditionSystem : EntitySystem
{
    private EntityQuery<MetaDataComponent> _metaQuery;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BecomeGolemConditionComponent, ObjectiveGetProgressEvent>(OnGetProgress);
        _metaQuery = GetEntityQuery<MetaDataComponent>();
    }

    private void OnGetProgress(EntityUid uid, BecomeGolemConditionComponent comp, ref ObjectiveGetProgressEvent args)
    {
        args.Progress = GetProgress(args.Mind);
    }

    private float GetProgress(MindComponent mind)
    {
        if (!_metaQuery.HasComp(mind.OwnedEntity))
            return 0;

        if(HasComp<GolemComponent>(mind.CurrentEntity))
            return 1;

        return 0;
    }
}
