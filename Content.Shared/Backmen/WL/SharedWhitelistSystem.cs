using Content.Shared.Species.Components;

namespace Content.Shared.Backmen.WL;

public abstract class SharedWhitelistSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
    }

    public abstract void ProcessReform(EntityUid child, Entity<ReformComponent> source);
}
