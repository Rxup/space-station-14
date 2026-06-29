using Content.Shared.Backmen.Companions;
using Robust.Shared.Map;

namespace Content.Server.Backmen.Companions;

public sealed partial class SpawnCompanionOnMapInitSystem : EntitySystem
{
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SpawnCompanionOnMapInitComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<SpawnCompanionOnMapInitComponent> ent, ref MapInitEvent args)
    {
        Spawn(ent.Comp.Companion, _transform.GetMapCoordinates(ent));
        QueueDel(ent);
    }
}
