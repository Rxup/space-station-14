using Content.Shared.Body.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Morgue.Components;
using Content.Shared.Standing;
using Content.Shared.Storage.Components;

namespace Content.Shared.Morgue;

public sealed class EntityStorageLayingDownOverrideSystem : EntitySystem
{
    [Dependency] private readonly StandingStateSystem _standing = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EntityStorageLayingDownOverrideComponent, StorageBeforeCloseEvent>(OnBeforeClose);
    }

    private void OnBeforeClose(EntityUid uid, EntityStorageLayingDownOverrideComponent component, ref StorageBeforeCloseEvent args)
    {
        foreach (var ent in args.Contents)
        {
            if (HasComp<BodyComponent>(ent) && (!_standing.IsDown(ent) || !_mobState.IsDead(ent)))
                args.Contents.Remove(ent);
        }
    }
}
