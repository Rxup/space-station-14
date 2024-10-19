using Content.Shared.Tools.Components;

namespace Content.Shared.Backmen.Arrivals;

public abstract class SharedArrivalsProtectSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ToolUserAttemptUseEvent>(OnTryUse);
    }

    private void OnTryUse(ref ToolUserAttemptUseEvent msg)
    {
        if (msg.Target == null)
        {
            return;
        }


        var pos = Transform(msg.Target!.Value);

        if (pos.GridUid == null || !HasComp<ArrivalsProtectGridComponent>(pos.GridUid))
        {
            return;
        }

        msg.Cancelled = true;
    }
}
