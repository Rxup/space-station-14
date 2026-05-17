using Content.Shared.Examine;
using Content.Shared.Interaction.Events;
using Content.Shared._Impstation.Revenant.Components;

namespace Content.Client._Impstation.Revenant;

public sealed partial class RevenantStasisSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RevenantStasisComponent, ChangeDirectionAttemptEvent>(OnAttemptDirection);
        SubscribeLocalEvent<RevenantStasisComponent, ExaminedEvent>(OnExamine);
    }

    private void OnExamine(EntityUid uid, RevenantStasisComponent comp, ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("revenant-stasis-regenerating"));
    }

    private void OnAttemptDirection(EntityUid uid, RevenantStasisComponent comp, ChangeDirectionAttemptEvent args)
    {
        args.Cancel();
    }
}
