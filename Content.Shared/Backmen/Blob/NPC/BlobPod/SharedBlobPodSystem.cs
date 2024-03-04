using Content.Shared.DoAfter;
using Content.Shared.Humanoid;
using Content.Shared.Mobs.Systems;
using Content.Shared.Verbs;
using Robust.Shared.Serialization;

namespace Content.Shared.Backmen.Blob.NPC.BlobPod;

public abstract class SharedBlobPodSystem : EntitySystem
{
    [Dependency] private readonly MobStateSystem _mobs = default!;

    private EntityQuery<HumanoidAppearanceComponent> _query;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BlobPodComponent, GetVerbsEvent<InnateVerb>>(AddDrainVerb);

        _query = GetEntityQuery<HumanoidAppearanceComponent>();
    }

    private void AddDrainVerb(EntityUid uid, BlobPodComponent component, GetVerbsEvent<InnateVerb> args)
    {
        if (args.User == args.Target)
            return;
        if (!args.CanAccess)
            return;
        if (!_query.HasComponent(args.Target))
            return;
        if (_mobs.IsAlive(args.Target))
            return;

        InnateVerb verb = new()
        {
            Act = () =>
            {
                NpcStartZombify(uid, args.Target, component);
            },
            Text = Loc.GetString("blob-pod-verb-zombify"),
            // Icon = new SpriteSpecifier.Texture(new ("/Textures/")),
            Priority = 2
        };
        args.Verbs.Add(verb);
    }

    public abstract bool NpcStartZombify(EntityUid uid, EntityUid argsTarget, BlobPodComponent component);
}


[Serializable, NetSerializable]
public sealed partial class BlobPodZombifyDoAfterEvent : SimpleDoAfterEvent
{
}
