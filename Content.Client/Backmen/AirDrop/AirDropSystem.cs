using Content.Client.Storage.Visualizers;
using Content.Shared.Backmen.AirDrop;
using Robust.Client.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client.Backmen.AirDrop;

public sealed class AirDropSystem : SharedAirDropSystem
{
    [Dependency] private readonly SpriteSystem _spriteSystem = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IComponentFactory _componentFactory = default!;
    [Dependency] private readonly AppearanceSystem _appearance = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AirDropVisualizerComponent, AfterAutoHandleStateEvent>(OnInit, after: [typeof(SpriteSystem)]);
        SubscribeLocalEvent<AirDropVisualizerComponent, AppearanceChangeEvent>(OnAppearance);
    }

    private void OnAppearance(Entity<AirDropVisualizerComponent> ent, ref AppearanceChangeEvent args)
    {
        Apply(ent);
    }

    private void OnInit(Entity<AirDropVisualizerComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        Apply(ent);
    }

    private void Apply(Entity<AirDropVisualizerComponent> ent)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        var proto = _prototype.Index(ent.Comp.SupplyDrop);
        if (!proto.TryGetComponent(out AirDropComponent? airDrop, _componentFactory))
            return;

        var renderedItem = Spawn(ent.Comp.SupplyDropOverride ?? airDrop.SupplyDropProto, airDrop.SupplyDrop);
        DebugTools.Assert(!HasComp<AirDropVisualizerComponent>(renderedItem), $"Удали AirDropVisualizerComponent c `{Prototype(renderedItem)?.ID}`");
        var renderedSprite = Comp<SpriteComponent>(renderedItem);

        _appearance.OnChangeData(renderedItem, renderedSprite);
        _spriteSystem.CopySprite((renderedItem, renderedSprite), (ent.Owner, sprite));
        QueueDel(renderedItem);
    }
}
