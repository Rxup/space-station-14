using Content.Server.Backmen.Psionics.Objectives.Components;
using Content.Shared.Backmen.Abilities.Psionics;
using Content.Shared.Mind;
using Content.Shared.Objectives.Components;

namespace Content.Server.Backmen.Psionics.Objectives;

public sealed class BecomePsionicConditionSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BecomePsionicConditionComponent, ObjectiveGetProgressEvent>(OnGetProgress);
    }

    private void OnGetProgress(Entity<BecomePsionicConditionComponent> ent, ref ObjectiveGetProgressEvent args)
    {
        args.Progress = GetProgress(args.Mind);
    }

    private float GetProgress(MindComponent mind)
    {
        return HasComp<PsionicComponent>(mind.OwnedEntity) ? 1 : 0;
    }
}
