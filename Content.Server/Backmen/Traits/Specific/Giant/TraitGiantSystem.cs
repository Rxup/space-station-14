using Robust.Server.Console;

namespace Content.Server.Backmen.Traits.Specific.Giant;

public sealed partial class TraitGiantSystem : EntitySystem
{
    [Dependency] private IServerConsoleHost _host = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TraitGiantComponent, MapInitEvent>(Scale);
    }

    private void Scale(Entity<TraitGiantComponent> ent, ref MapInitEvent args)
    {
        if (ent.Comp.Scale <= 0 || !ent.Owner.Valid)
        {
            Log.Log(LogLevel.Warning, $"invalid parameters for TraitGiantComponent. EntId: {ent.Owner}, scale: {ent.Comp.Scale}");
            return;
        }

        _host.ExecuteCommand(null, $"scale {ent.Owner} {ent.Comp.Scale}");
        RemCompDeferred<TraitGiantComponent>(ent);
    }
}
