using Robust.Server.Console;
using Robust.Server.GameObjects;

namespace Content.Server.Backmen.Traits.Specific.Giant;

public sealed class TraitGiantSystem : EntitySystem
{
    [Dependency] private readonly IServerConsoleHost _host = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TraitGiantComponent, MapInitEvent>(Scale);
    }

    private void Scale(Entity<TraitGiantComponent> ent, ref MapInitEvent args)
    {
        _host.ExecuteCommand(null, $"scale {ent.Owner} {ent.Comp.Scale}");
        RemCompDeferred<TraitGiantComponent>(ent);
    }
}
