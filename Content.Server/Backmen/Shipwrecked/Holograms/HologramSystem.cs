using Content.Shared.Storage.Components;

namespace Content.Server.Backmen.Shipwrecked.Holograms;

public sealed class HologramSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HologramComponent, StoreMobInItemContainerAttemptEvent>(OnStoreInContainerAttempt);
    }

    private void OnStoreInContainerAttempt(EntityUid uid, HologramComponent component, ref StoreMobInItemContainerAttemptEvent args)
    {
        // TODO: It should be okay to move this to Shared.
        // Forbid holograms from going inside anything.
        args.Cancelled = true;
        args.Handled = true;
    }
}
