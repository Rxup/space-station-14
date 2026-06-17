using System.Linq;
using Content.Client.Storage.Visualizers;
using Content.Shared.Backmen.AirDrop;
using Robust.Client.Animations;
using Robust.Client.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client.Backmen.AirDrop;

public sealed partial class AirDropSystem : SharedAirDropSystem
{
    private const string TargetAnimKey = "airdrop-target";
    private const string FallingAnimKey = "airdrop-falling";
    /// <summary>
    /// Fallback when the prototype layer has no <c>map:</c> in YAML (then <see cref="StorageVisualLayers.Base"/> is absent).
    /// </summary>
    private const string VisualLayerKey = "airdrop-visual";
    private const int VisualLayerIndex = 0;

    [Dependency] private AnimationPlayerSystem _animation = default!;
    [Dependency] private SpriteSystem _spriteSystem = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private IComponentFactory _componentFactory = default!;
    [Dependency] private AppearanceSystem _appearance = default!;

    private readonly Queue<Entity<AirDropVisualizerComponent>> _updateQueue = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AirDropComponent, ComponentShutdown>(OnAirDropShutdown);
        SubscribeLocalEvent<AirDropVisualizerComponent, AfterAutoHandleStateEvent>(OnUpdate,
            after: [typeof(SpriteSystem)]);
        SubscribeNetworkEvent<AirDropStartEvent>(OnStartAirDrop);

        UpdatesAfter.Add(typeof(SpriteSystem));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<AirDropComponent, MetaDataComponent>();
        while (query.MoveNext(out var uid, out var comp, out var meta))
        {
            if (meta.EntityPaused)
                continue;

            SyncVisuals((uid, comp));
        }
    }

    public override void FrameUpdate(float frameTime)
    {
        while (_updateQueue.TryDequeue(out var ent))
        {
            Apply(ent);
        }
    }

    private void OnStartAirDrop(AirDropStartEvent ev)
    {
        if (!TryGetEntity(ev.Uid, out var uid) || !TryComp<AirDropComponent>(uid, out var comp))
            return;

        // Update() may have already synced visuals from replicated phase; do not restart.
        if (comp.ClientLastPhase != AirDropPhase.Inactive || comp.Phase == AirDropPhase.Inactive)
            return;

        SyncVisuals((uid.Value, comp));
    }

    private void OnAirDropShutdown(Entity<AirDropComponent> ent, ref ComponentShutdown args)
    {
        ent.Comp.ClientLastPhase = AirDropPhase.Inactive;
    }

    private void SyncVisuals(Entity<AirDropComponent> ent)
    {
        if (ent.Comp.ClientLastPhase == ent.Comp.Phase)
        {
            EnsureVisualsForPhase(ent);
            return;
        }

        var previous = ent.Comp.ClientLastPhase;
        ent.Comp.ClientLastPhase = ent.Comp.Phase;

        switch (ent.Comp.Phase)
        {
            case AirDropPhase.Target:
                EnsureTargetAnimation(ent);
                break;

            case AirDropPhase.Drop:
                EnsureTargetAnimation(ent);
                EnsureFallingAnimation(ent);
                break;

            default:
                if (previous is AirDropPhase.Target or AirDropPhase.Drop)
                    ent.Comp.ClientLastPhase = ent.Comp.Phase;
                break;
        }
    }

    private void EnsureTargetAnimation(Entity<AirDropComponent> ent)
    {
        if (ent.Comp.TargetMarker is not { } marker || !TryComp<SpriteComponent>(marker, out var sprite))
            return;

        ApplyEffectSpriteSettings(sprite);
        PlayTargetMarkerLoop(marker, sprite);
    }

    private void EnsureFallingAnimation(Entity<AirDropComponent> ent)
    {
        if (ent.Comp.InAirMarker is not { } marker || !TryComp<SpriteComponent>(marker, out var sprite))
            return;

        ApplyEffectSpriteSettings(sprite);
        PlaySpriteAnimation(marker, sprite, FallingAnimKey, ent.Comp.TimeToDrop);
    }

    private void EnsureVisualsForPhase(Entity<AirDropComponent> ent)
    {
        switch (ent.Comp.Phase)
        {
            case AirDropPhase.Target:
                EnsureTargetAnimation(ent);
                break;

            case AirDropPhase.Drop:
                EnsureTargetAnimation(ent);
                EnsureFallingAnimation(ent);
                break;
        }
    }

    private void ApplyEffectSpriteSettings(SpriteComponent sprite)
    {
        // Upright on screen (same idea as the holographic marker's noRot).
        sprite.NoRotation = true;
    }

    /// <summary>
    /// Uses <see cref="StorageVisualLayers.Base"/> when the prototype defines <c>map:</c> on a layer; otherwise index 0 + fallback key.
    /// </summary>
    private bool TryResolveAnimationLayer(Entity<SpriteComponent?> ent, out object layerKey, out int layerIndex)
    {
        layerKey = VisualLayerKey;
        layerIndex = VisualLayerIndex;

        if (_spriteSystem.LayerMapTryGet(ent, StorageVisualLayers.Base, out layerIndex, false))
        {
            layerKey = StorageVisualLayers.Base;
            return true;
        }

        if (_spriteSystem.LayerMapTryGet(ent, "Base", out layerIndex, false))
        {
            layerKey = "Base";
            return true;
        }

        if (!ent.Comp!.AllLayers.Any())
            return false;

        if (!_spriteSystem.LayerMapTryGet(ent, VisualLayerKey, out _, false))
            _spriteSystem.LayerMapSet(ent, VisualLayerKey, layerIndex);

        layerKey = VisualLayerKey;
        return true;
    }

    private void PlayTargetMarkerLoop(EntityUid uid, SpriteComponent sprite)
    {
        var ent = (uid, sprite);
        if (!TryResolveAnimationLayer(ent, out _, out var layerIndex))
            return;

        _spriteSystem.LayerSetAutoAnimated(ent, layerIndex, true);
    }

    private void PlaySpriteAnimation(EntityUid uid, SpriteComponent sprite, string animKey, float durationSeconds)
    {
        var ent = (uid, sprite);
        if (!TryResolveAnimationLayer(ent, out var layerKey, out var layerIndex))
            return;

        var player = EnsureComp<AnimationPlayerComponent>(uid);
        if (_animation.HasRunningAnimation(uid, player, animKey))
            return;

        _spriteSystem.LayerSetAutoAnimated(ent, layerIndex, false);

        var state = _spriteSystem.LayerGetRsiState(ent, layerIndex);
        var anim = new Animation
        {
            Length = TimeSpan.FromSeconds(durationSeconds),
            AnimationTracks =
            {
                new AnimationTrackSpriteFlick
                {
                    LayerKey = layerKey,
                    KeyFrames =
                    {
                        new AnimationTrackSpriteFlick.KeyFrame(state.Name, 0f),
                    }
                }
            }
        };

        _animation.Play((uid, player), anim, animKey);
    }

    private void OnUpdate(Entity<AirDropVisualizerComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        _updateQueue.Enqueue(ent);
    }

    private void Apply(Entity<AirDropVisualizerComponent> ent)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        var proto = _prototype.Index(ent.Comp.SupplyDrop);
        if (!proto.TryGetComponent(out AirDropComponent? airDrop, _componentFactory))
            return;

        var renderedItem = Spawn(ent.Comp.SupplyDropOverride ?? airDrop.SupplyDropProto, airDrop.SupplyDrop);
        DebugTools.Assert(!HasComp<AirDropVisualizerComponent>(renderedItem),
            $"Удали AirDropVisualizerComponent c `{Prototype(renderedItem)?.ID}`");
        var renderedSprite = Comp<SpriteComponent>(renderedItem);
        _appearance.OnChangeData(renderedItem, renderedSprite);
        foreach (var (compName, comp) in airDrop.SupplyDrop)
        {
            if (compName == "Sprite")
                continue;

            if (EntityManager.TryGetComponent(renderedItem, comp.Component.GetType(), out var sourceComp))
                EntityManager.CopyComponent(renderedItem, ent, sourceComp);
        }

        _spriteSystem.CopySprite((renderedItem, renderedSprite), (ent.Owner, sprite));
        QueueDel(renderedItem);
        _appearance.OnChangeData(ent.Owner, sprite);
    }
}
