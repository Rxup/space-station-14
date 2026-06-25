using Content.Client.Clothing;
using Content.Shared.Backmen.Arachne;
using Content.Shared.Body;
using Content.Shared.Buckle.Components;
using Content.Shared.Clothing;
using Content.Shared.Clothing.Components;
using Content.Shared.Clothing.Events;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Rotation;
using Robust.Client.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Client.Backmen.Species.Arachne;

/// <summary>
/// Client visuals for surgical and roundstart arachne: sprite layer order, clothing stencil mask.
/// </summary>
public sealed partial class ArachneSpriteLayerSystem : EntitySystem
{
    private static readonly ProtoId<SpeciesPrototype> ArachneClassicSpecies = "ArachneClassic";

    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private ClientClothingSystem _clothing = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    private readonly HashSet<EntityUid> _pending = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ArachneComponent, ComponentStartup>(OnArachneStartup);
        SubscribeLocalEvent<ArachneComponent, OrganInsertedIntoEvent>(OnOrganInserted);
        SubscribeLocalEvent<ArachneComponent, OrganRemovedFromEvent>(OnOrganRemoved);
        SubscribeLocalEvent<ArachneGraftVisualComponent, OrganGotInsertedEvent>(OnGraftVisualInserted);
        SubscribeLocalEvent<ArachneGraftVisualComponent, OrganGotRemovedEvent>(OnGraftVisualRemoved);
        SubscribeLocalEvent<ArachneComponent, BuckledEvent>(OnArachneBuckled);
        SubscribeLocalEvent<RotationVisualsComponent, BuckledEvent>(OnRotationVisualsBuckled);

        SubscribeLocalEvent<ArachneClothingStencilComponent, ComponentStartup>(OnStencilStartup);
        SubscribeLocalEvent<ArachneClothingStencilComponent, BeforeClothingAppearanceRefreshEvent>(OnBeforeClothingAppearance);
        SubscribeLocalEvent<ArachneClothingStencilComponent, AfterClothingAppearanceRefreshEvent>(OnAfterClothingAppearance);
        SubscribeLocalEvent<ArachneClothingStencilComponent, DidUnequipEvent>(OnDidUnequip);
        SubscribeLocalEvent<ClothingComponent, EquipmentVisualsUpdatedEvent>(OnEquipmentVisualsUpdated);
    }

    private void OnOrganInserted(Entity<ArachneComponent> ent, ref OrganInsertedIntoEvent args) =>
        QueueLayout(ent);

    private void OnOrganRemoved(Entity<ArachneComponent> ent, ref OrganRemovedFromEvent args) =>
        QueueLayout(ent);

    private void OnGraftVisualInserted(Entity<ArachneGraftVisualComponent> ent, ref OrganGotInsertedEvent args) =>
        QueueLayout(args.Target);

    private void OnGraftVisualRemoved(Entity<ArachneGraftVisualComponent> ent, ref OrganGotRemovedEvent args) =>
        QueueLayout(args.Target);

    private void OnArachneBuckled(Entity<ArachneComponent> ent, ref BuckledEvent args) =>
        QueueLayout(ent);

    private void OnRotationVisualsBuckled(Entity<RotationVisualsComponent> ent, ref BuckledEvent args)
    {
        if (HasArachneGraftVisual(ent))
            QueueLayout(ent);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        if (_pending.Count == 0)
            return;

        foreach (var uid in _pending)
        {
            if (Deleted(uid))
                continue;

            TryApplyLayout(uid);
        }

        _pending.Clear();
    }

    private void OnArachneStartup(Entity<ArachneComponent> ent, ref ComponentStartup args) =>
        QueueLayout(ent);

    private void QueueLayout(EntityUid uid) =>
        _pending.Add(uid);

    private void TryApplyLayout(EntityUid uid)
    {
        if (!TryComp(uid, out SpriteComponent? sprite) || !HasArachneGraftVisual(uid))
            return;

        if (IsRoundstartArachne(uid))
            return;

        if (!NeedsLayout(uid, sprite))
            return;

        ApplyLayout(uid, sprite);
    }

    private bool IsRoundstartArachne(EntityUid uid) =>
        CompOrNull<HumanoidProfileComponent>(uid)?.Species == ArachneClassicSpecies;

    private bool HasArachneGraftVisual(EntityUid uid)
    {
        if (HasComp<ArachneComponent>(uid))
            return true;

        if (!TryComp(uid, out BodyComponent? body) || body.Organs == null)
            return false;

        foreach (var organ in body.Organs.ContainedEntities)
        {
            if (HasComp<ArachneGraftVisualComponent>(organ))
                return true;
        }

        return false;
    }

    private bool NeedsLayout(EntityUid uid, SpriteComponent sprite)
    {
        var ent = (uid, sprite);

        if (!_sprite.LayerMapTryGet(ent, HumanoidVisualLayers.LLeg, out var lLeg, false)
            || !_sprite.LayerMapTryGet(ent, HumanoidVisualLayers.Chest, out var chest, false))
        {
            return false;
        }

        if (lLeg >= chest)
            return true;

        if (_sprite.LayerMapTryGet(ent, HumanoidVisualLayers.RLeg, out var rLeg, false)
            && rLeg >= chest)
        {
            return true;
        }

        if (!_sprite.LayerMapTryGet(ent, HumanoidVisualLayers.StencilMask, out _, false))
            return true;

        if (_sprite.LayerMapTryGet(ent, ClientClothingSystem.Jumpsuit, out var jumpsuit, false)
            && _sprite.LayerMapTryGet(ent, HumanoidVisualLayers.LHand, out var lHand, false)
            && lHand < jumpsuit)
        {
            return true;
        }

        return false;
    }

    private void ApplyLayout(EntityUid uid, SpriteComponent sprite)
    {
        var ent = (uid, sprite);

        MoveMappedLayerBefore(ent, HumanoidVisualLayers.LLeg, HumanoidVisualLayers.Chest);
        MoveMappedLayerBefore(ent, HumanoidVisualLayers.RLeg, HumanoidVisualLayers.Chest);

        EnsureStencilLayers(ent);
        SyncClothingStencil(uid, sprite);

        if (_sprite.LayerMapTryGet(ent, ClientClothingSystem.Jumpsuit, out var jumpsuit, false)
            && _sprite.LayerMapTryGet(ent, HumanoidVisualLayers.LHand, out var lHand, false)
            && lHand < jumpsuit)
        {
            MoveMappedLayerAfter(ent, HumanoidVisualLayers.LHand, ClientClothingSystem.Jumpsuit);
            MoveMappedLayerAfter(ent, HumanoidVisualLayers.RHand, HumanoidVisualLayers.LHand);
        }
    }

    private void EnsureStencilLayers(Entity<SpriteComponent?> ent)
    {
        if (_sprite.LayerMapTryGet(ent, HumanoidVisualLayers.StencilMask, out _, false))
            return;

        if (!_sprite.LayerMapTryGet(ent, HumanoidVisualLayers.UndergarmentBottom, out var insertAt, false)
            && !_sprite.LayerMapTryGet(ent, ClientClothingSystem.Jumpsuit, out insertAt, false)
            && !_sprite.LayerMapTryGet(ent, HumanoidVisualLayers.LArm, out insertAt, false))
        {
            return;
        }

        _sprite.AddLayer(ent, new PrototypeLayerData
        {
            Shader = "StencilClear",
            RsiPath = "Mobs/Species/Human/parts.rsi",
            State = "l_leg",
        }, insertAt);

        var stencilState = ComputeStencilState(ent.Owner);
        if (!ArachneClothingStencilVisuals.TryGetLayerData(stencilState, out var maskData))
        {
            maskData = new PrototypeLayerData
            {
                Shader = "StencilMask",
                RsiPath = ArachneClothingStencilVisuals.MaskSprite,
                State = "unsexed_full",
                Visible = false,
            };
        }

        var maskIndex = _sprite.AddLayer(ent, maskData, insertAt + 1);

        _sprite.LayerMapSet(ent, HumanoidVisualLayers.StencilMask, maskIndex);
        _sprite.LayerSetVisible(ent, maskIndex, maskData.Visible ?? false);
        EnsureComp<ArachneClothingStencilComponent>(ent.Owner);
    }

    /// <summary>
    /// Recomputes the anytaur clothing stencil for the body's sex and refreshes equipped clothing.
    /// Needed when the stencil is added after map init (e.g. surgical graft).
    /// </summary>
    private void SyncClothingStencil(EntityUid uid, SpriteComponent sprite)
    {
        if (!HasComp<ArachneClothingStencilComponent>(uid))
            return;

        RefreshStencilAppearance(uid);
        SyncClothingStencilMask(uid, sprite);

        if (TryComp(uid, out InventoryComponent? inventory))
            _clothing.InitClothing(uid, inventory);
    }

    private void SyncClothingStencilMask(EntityUid uid, SpriteComponent sprite)
    {
        var state = _appearance.TryGetData(uid, ArachneVisuals.ClothingStencil, out ArachneClothingStencilState appearanceState)
            ? appearanceState
            : ComputeStencilState(uid);

        ApplyStencilMask(uid, sprite, state);
    }

    private bool MoveMappedLayerBefore(Entity<SpriteComponent?> ent, Enum layerKey, Enum beforeKey)
    {
        if (!_sprite.LayerMapTryGet(ent, layerKey, out var from, false)
            || !_sprite.LayerMapTryGet(ent, beforeKey, out var to, false)
            || from == to - 1)
        {
            return false;
        }

        if (!_sprite.RemoveLayer(ent, layerKey, out var layer, false))
            return false;

        if (!_sprite.LayerMapTryGet(ent, beforeKey, out to, false))
        {
            var index = _sprite.AddLayer(ent, layer);
            _sprite.LayerMapSet(ent, layerKey, index);
            return false;
        }

        var newIndex = _sprite.AddLayer(ent, layer, to);
        _sprite.LayerMapSet(ent, layerKey, newIndex);
        return true;
    }

    private bool MoveMappedLayerAfter(Entity<SpriteComponent?> ent, Enum layerKey, Enum afterKey)
    {
        if (!_sprite.LayerMapTryGet(ent, layerKey, out var from, false)
            || !_sprite.LayerMapTryGet(ent, afterKey, out var after, false)
            || from == after + 1)
        {
            return false;
        }

        if (!_sprite.RemoveLayer(ent, layerKey, out var layer, false))
            return false;

        if (!_sprite.LayerMapTryGet(ent, afterKey, out after, false))
        {
            var index = _sprite.AddLayer(ent, layer);
            _sprite.LayerMapSet(ent, layerKey, index);
            return false;
        }

        var newIndex = _sprite.AddLayer(ent, layer, after + 1);
        _sprite.LayerMapSet(ent, layerKey, newIndex);
        return true;
    }

    private bool MoveMappedLayerAfter(Entity<SpriteComponent?> ent, Enum layerKey, string afterKey)
    {
        if (!_sprite.LayerMapTryGet(ent, layerKey, out var from, false)
            || !_sprite.LayerMapTryGet(ent, afterKey, out var after, false)
            || from == after + 1)
        {
            return false;
        }

        if (!_sprite.RemoveLayer(ent, layerKey, out var layer, false))
            return false;

        if (!_sprite.LayerMapTryGet(ent, afterKey, out after, false))
        {
            var index = _sprite.AddLayer(ent, layer);
            _sprite.LayerMapSet(ent, layerKey, index);
            return false;
        }

        var newIndex = _sprite.AddLayer(ent, layer, after + 1);
        _sprite.LayerMapSet(ent, layerKey, newIndex);
        return true;
    }

    private void OnStencilStartup(Entity<ArachneClothingStencilComponent> ent, ref ComponentStartup args)
    {
        if (!TryComp(ent, out SpriteComponent? sprite))
        {
            RefreshStencilAppearance(ent);
            return;
        }

        SyncClothingStencil(ent, sprite);
    }

    private void OnBeforeClothingAppearance(Entity<ArachneClothingStencilComponent> ent, ref BeforeClothingAppearanceRefreshEvent args)
    {
        if (!TryComp(ent, out SpriteComponent? sprite))
            return;

        if (_sprite.LayerMapTryGet((ent, sprite), HumanoidVisualLayers.StencilMask, out var layer, false))
            _sprite.LayerSetVisible((ent, sprite), layer, false);
    }

    private void OnAfterClothingAppearance(Entity<ArachneClothingStencilComponent> ent, ref AfterClothingAppearanceRefreshEvent args)
    {
        if (!TryComp(ent, out SpriteComponent? sprite))
            return;

        if (!_appearance.TryGetData(ent, ArachneVisuals.ClothingStencil, out ArachneClothingStencilState state))
            state = ComputeStencilState(ent);

        ApplyStencilMask(ent, sprite, state);
    }

    private void OnDidUnequip(Entity<ArachneClothingStencilComponent> ent, ref DidUnequipEvent args)
    {
        if (args.Slot != ClientClothingSystem.Jumpsuit)
            return;

        RefreshStencilAppearance(ent);
    }

    private void OnEquipmentVisualsUpdated(Entity<ClothingComponent> ent, ref EquipmentVisualsUpdatedEvent args)
    {
        if (!HasComp<ArachneClothingStencilComponent>(args.Equipee)
            || !TryComp(args.Equipee, out SpriteComponent? sprite))
            return;

        if (args.Slot == ClientClothingSystem.Jumpsuit)
            RefreshStencilAppearance(args.Equipee);

        if (!UsesStencilMask(args.Slot)
            || !_sprite.LayerMapTryGet((args.Equipee, sprite), HumanoidVisualLayers.StencilMask, out _, false))
            return;

        foreach (var layerKey in args.RevealedLayers)
        {
            if (!_sprite.LayerMapTryGet((args.Equipee, sprite), layerKey, out var index, false))
                continue;

            sprite.LayerSetShader(index, "StencilDraw");
        }
    }

    private void RefreshStencilAppearance(EntityUid uid)
    {
        if (!HasComp<ArachneClothingStencilComponent>(uid))
            return;

        _appearance.SetData(uid, ArachneVisuals.ClothingStencil, ComputeStencilState(uid));
    }

    private ArachneClothingStencilState ComputeStencilState(EntityUid uid)
    {
        if (!TryComp(uid, out InventoryComponent? inventory)
            || !_inventory.TryGetSlotEntity(uid, ClientClothingSystem.Jumpsuit, out var suit, inventory)
            || !TryComp(suit, out ClothingComponent? clothing))
        {
            return ArachneClothingStencilState.Hidden;
        }

        var sex = CompOrNull<HumanoidProfileComponent>(uid)?.Sex;
        var mask = sex switch
        {
            Sex.Male => clothing.MaleMask,
            Sex.Female => clothing.FemaleMask,
            _ => clothing.UnisexMask,
        };

        return ArachneClothingStencilVisuals.GetState(sex, mask);
    }

    private static bool UsesStencilMask(string slot) =>
        slot is ClientClothingSystem.Jumpsuit or "underpants" or "undershirt" or "socks";

    private void ApplyStencilMask(EntityUid uid, SpriteComponent sprite, ArachneClothingStencilState state)
    {
        if (!_sprite.LayerMapTryGet((uid, sprite), HumanoidVisualLayers.StencilMask, out var layer, false)
            || !ArachneClothingStencilVisuals.TryGetLayerData(state, out var data))
        {
            return;
        }

        _sprite.LayerSetData((uid, sprite), layer, data);
    }
}
