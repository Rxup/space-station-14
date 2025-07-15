using Content.Client.Storage.Visualizers;
using Content.Shared.Backmen.AirDrop;
using Robust.Client.ComponentTrees;
using Robust.Client.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.Backmen.AirDrop;

public sealed class AirDropSystem : SharedAirDropSystem
{
    [Dependency] private readonly SpriteSystem _spriteSystem = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IComponentFactory _componentFactory = default!;
    [Dependency] private readonly AppearanceSystem _appearance = default!;

    private readonly Queue<Entity<AirDropVisualizerComponent>> _updateQueue = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AirDropVisualizerComponent, AfterAutoHandleStateEvent>(OnUpdate,
            after: [typeof(SpriteSystem)]);
        SubscribeNetworkEvent<AirDropStartEvent>(OnStartAirDrop);

        UpdatesAfter.Add(typeof(SpriteSystem));
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
        if (!TryGetEntity(ev.Uid, out var supplyPod))
            return;
        StartAirDrop(supplyPod.Value, ev.Pos);
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
        foreach (var (compName,comp) in airDrop.SupplyDrop)
        {
            if(compName == "Sprite")
                continue;

            if(EntityManager.TryGetComponent(renderedItem, comp.Component.GetType(), out var sourceComp))
                EntityManager.CopyComponent(renderedItem, ent, sourceComp);
        }
        _spriteSystem.CopySprite((renderedItem, renderedSprite), (ent.Owner, sprite));
        QueueDel(renderedItem);
        _appearance.OnChangeData(ent.Owner, sprite);
    }
}
