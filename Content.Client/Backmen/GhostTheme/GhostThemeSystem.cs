using Content.Client.Clothing;
using Content.Client.Hands.Systems;
using Content.Client.Inventory;
using Content.Shared.Backmen.GhostTheme;
using Content.Shared.Hands.Components;
using Content.Shared.Inventory;
using Content.Shared.Item;
using Robust.Client.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Client.Backmen.GhostTheme;

public sealed partial class GhostThemeSystem : EntitySystem
{
    private const string GhostVariantKey = "ghostVariant";

    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private SpriteSystem _spriteSystem = default!;
    [Dependency] private HandsSystem _hands = default!;
    [Dependency] private ClientClothingSystem _clothing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GhostThemeComponent, AfterAutoHandleStateEvent>(OnInit);
    }

    private void OnInit(EntityUid uid, GhostThemeComponent component, ref AfterAutoHandleStateEvent args)
    {
        if (component.GhostTheme == null
            || !_prototypeManager.TryIndex<GhostThemePrototype>(component.GhostTheme, out var ghostThemePrototype))
        {
            return;
        }

        Apply(uid, ghostThemePrototype);
    }

    private readonly EntProtoId MobObserver = "MobObserver";

    public void Apply(EntityUid uid, GhostThemePrototype ghostThemePrototype)
    {
        var rendered = Spawn(MobObserver, ghostThemePrototype.Components);
        if (!TryComp(rendered, out SpriteComponent? source) ||
            !TryComp(uid, out SpriteComponent? target))
        {
            QueueDel(rendered);
            return;
        }

        ApplyThemeSprite(uid, target, rendered, source);
        RefreshSpriteOverlays(uid);
        QueueDel(rendered);
    }

    /// <summary>
    /// Updates only the ghost theme appearance. A full <see cref="SpriteSystem.CopySprite"/> would wipe
    /// dynamic layers (inhands, clothing, etc.) and cause "Layer with key does not exist" errors when
    /// hands/clothing systems later try to remove tracked layers.
    /// </summary>
    private void ApplyThemeSprite(EntityUid uid, SpriteComponent target, EntityUid sourceUid, SpriteComponent source)
    {
        if (_spriteSystem.LayerMapTryGet((sourceUid, source), GhostVariantKey, out var srcIdx, false)
            && _spriteSystem.TryGetLayer((sourceUid, source), srcIdx, out var srcLayer, false))
        {
            var data = srcLayer.ToPrototypeData();
            // Bake base RSI into the layer so SetBaseRsi order cannot leave ghostVariant on a missing state.
            if (data.RsiPath == null && source.BaseRSI != null)
                data.RsiPath = source.BaseRSI.Path.CanonPath;

            var dstIdx = _spriteSystem.LayerMapReserve((uid, target), GhostVariantKey);
            _spriteSystem.LayerSetData((uid, target), dstIdx, data);
        }

        _spriteSystem.SetBaseRsi((uid, target), source.BaseRSI);
        _spriteSystem.SetColor((uid, target), source.Color);
        _spriteSystem.SetDrawDepth((uid, target), source.DrawDepth);
        target.NoRotation = source.NoRotation;
        target.OverrideContainerOcclusion = source.OverrideContainerOcclusion;
    }

    /// <summary>
    /// Re-applies hand/clothing overlays after a theme change without calling RemoveLayer on missing keys.
    /// </summary>
    private void RefreshSpriteOverlays(EntityUid uid)
    {
        if (TryComp(uid, out HandsComponent? hands))
        {
            foreach (var layers in hands.RevealedLayers.Values)
                layers.Clear();

            foreach (var handId in hands.SortedHands)
            {
                if (!_hands.TryGetHeldItem((uid, hands), handId, out var held))
                    continue;

                RaiseLocalEvent(uid, new VisualsChangedEvent(GetNetEntity(held.Value), handId));
            }
        }

        if (TryComp(uid, out InventorySlotsComponent? slots))
        {
            foreach (var layers in slots.VisualLayerKeys.Values)
                layers.Clear();
        }

        if (TryComp(uid, out InventoryComponent? inventory))
            _clothing.InitClothing(uid, inventory);
    }
}
