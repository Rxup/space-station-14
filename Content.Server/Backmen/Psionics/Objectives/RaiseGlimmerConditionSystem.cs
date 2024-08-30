using Content.Server.Backmen.Psionics.Objectives.Components;
using Content.Shared.Backmen.Psionics.Glimmer;
using Content.Shared.Objectives.Components;

namespace Content.Server.Backmen.Psionics.Objectives;

public sealed class RaiseGlimmerConditionSystem : EntitySystem
{
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly GlimmerSystem _glimmer = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RaiseGlimmerConditionComponent, ObjectiveGetProgressEvent>(OnGetProgress);
        SubscribeLocalEvent<RaiseGlimmerConditionComponent, ObjectiveAfterAssignEvent>(OnAfterAssign);
    }

    private void OnAfterAssign(EntityUid uid, RaiseGlimmerConditionComponent comp, ref ObjectiveAfterAssignEvent args)
    {
        var title = Loc.GetString("objective-condition-raise-glimmer-title", ("target", comp.Target));
        var description = Loc.GetString("objective-condition-raise-glimmer-description", ("target", comp.Target));

        _metaData.SetEntityName(uid, title, args.Meta);
        _metaData.SetEntityDescription(uid, description, args.Meta);
    }

    private void OnGetProgress(EntityUid uid, RaiseGlimmerConditionComponent comp, ref ObjectiveGetProgressEvent args)
    {
        args.Progress = GetProgress(comp.Target);
    }

    private float GetProgress(int target)
    {
        var glimmer = _glimmer.Glimmer;
        var progress = Math.Min((float) glimmer / (float) target, 1f);
        return progress;
    }
}
