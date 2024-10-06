using Content.Shared.Chat.TypingIndicator;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Prototypes;
using Content.Shared.Inventory;

namespace Content.Client.Chat.TypingIndicator;

public sealed class TypingIndicatorVisualizerSystem : VisualizerSystem<TypingIndicatorComponent>
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TypingIndicatorComponent, AfterAutoHandleStateEvent>(OnChangeState); // backmen: TypingIndicator
    }

    // startbackmen: TypingIndicator
    private void OnChangeState(Entity<TypingIndicatorComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        UpdateAppearance(ent,ent);
    }

    private void UpdateAppearance(EntityUid uid,
        TypingIndicatorComponent component,
        AppearanceComponent? appearance = null,
        SpriteComponent? sprite = null)
    {
        if (!Resolve(uid, ref appearance, ref sprite, false))
            return;

        var currentTypingIndicator = component.TypingIndicatorPrototype;

        var evt = new BeforeShowTypingIndicatorEvent();

        if (TryComp<InventoryComponent>(uid, out var inventoryComp))
            _inventory.RelayEvent((uid, inventoryComp), ref evt);

        var overrideIndicator = evt.GetMostRecentIndicator();

        if (overrideIndicator != null)
            currentTypingIndicator = overrideIndicator.Value;

        if (!_prototypeManager.TryIndex(currentTypingIndicator, out var proto))
        {
            Log.Error($"Unknown typing indicator id: {component.TypingIndicatorPrototype}");
            return;
        }

        //AppearanceSystem.TryGetData<bool>(uid, TypingIndicatorVisuals.IsTyping, out var isTyping, args.Component); // Corvax-TypingIndicator
        var layerExists = sprite.LayerMapTryGet(TypingIndicatorLayers.Base, out var layer);
        if (!layerExists)
            layer = sprite.LayerMapReserveBlank(TypingIndicatorLayers.Base);

        sprite.LayerSetRSI(layer, proto.SpritePath);
        sprite.LayerSetState(layer, proto.TypingState);
        sprite.LayerSetShader(layer, proto.Shader);
        sprite.LayerSetOffset(layer, proto.Offset);
        // args.Sprite.LayerSetVisible(layer, isTyping); // Corvax-TypingIndicator

        // Corvax-TypingIndicator-Start
        sprite.LayerSetVisible(layer, component.TypingIndicatorState != TypingIndicatorState.None);
        switch (component.TypingIndicatorState)
        {
            case TypingIndicatorState.Idle:
                sprite.LayerSetState(layer, proto.IdleState);
                break;
            case TypingIndicatorState.Typing:
                sprite.LayerSetState(layer, proto.TypingState);
                break;
        }
        // Corvax-TypingIndicator-End
    }

    // end-backmen: TypingIndicator

    protected override void OnAppearanceChange(EntityUid uid, TypingIndicatorComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        UpdateAppearance(uid, component, args.Component, args.Sprite); // backmen: TypingIndicator
    }
}
