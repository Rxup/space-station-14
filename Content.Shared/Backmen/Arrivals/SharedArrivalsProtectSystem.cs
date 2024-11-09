using Content.Shared.Tools.Components;

namespace Content.Shared.Backmen.Arrivals;

public abstract class SharedArrivalsProtectSystem : EntitySystem
{
    protected EntityQuery<ArrivalsProtectGridComponent> ArrivalsProtectGridQuery;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ToolUserAttemptUseEvent>(OnTryUse);
        SubscribeLocalEvent<ArrivalsProtectGridComponent, FlatPackUserAttemptUseEvent>(OnTryUnpack);
        ArrivalsProtectGridQuery = GetEntityQuery<ArrivalsProtectGridComponent>();
    }

    private void OnTryUnpack(Entity<ArrivalsProtectGridComponent> ent, ref FlatPackUserAttemptUseEvent args)
    {
        args.Cancelled = true;
    }


    private void OnTryUse(ref ToolUserAttemptUseEvent msg)
    {
        if (msg.Target == null)
        {
            return;
        }


        var pos = Transform(msg.Target!.Value);

        if (pos.GridUid == null || !ArrivalsProtectGridQuery.HasComp(pos.GridUid))
        {
            return;
        }

        msg.Cancelled = true;
    }
}
