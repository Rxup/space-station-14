using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Content.Client.Alerts;
using Content.Client.Clothing;
using Content.Shared.Alert;
using Content.Shared.Backmen.Flesh;
using Content.Shared.Clothing;
using Content.Shared.Clothing.Components;
using Content.Shared.Clothing.EntitySystems;
using Content.Shared.Inventory;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations;
using Robust.Shared.Utility;

namespace Content.Client.Backmen.Flesh;

/// <summary>
/// Client visuals for face-attached flesh worms and headcrabs.
/// </summary>
public sealed partial class FleshWormVisualSystem : EntitySystem
{
    private static readonly ProtoId<AlertPrototype> SuffocationAlert = "FleshWormSuffocation";

    [Dependency] private IResourceCache _cache = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FleshWormComponent, GetEquipmentVisualsEvent>(OnGetEquipmentVisuals,
            after: [typeof(ClientClothingSystem)]);
        SubscribeLocalEvent<FleshWormSuffocationAlertComponent, UpdateAlertSpriteEvent>(OnUpdateSuffocationAlertSprite);
    }

    private void OnUpdateSuffocationAlertSprite(
        Entity<FleshWormSuffocationAlertComponent> ent,
        ref UpdateAlertSpriteEvent args)
    {
        if (args.Alert.ID != SuffocationAlert)
            return;

        if (!TryGetFaceAttacher(args.ViewerEnt, out var attacher)
            || !TryComp<SpriteComponent>(attacher, out var attackerSprite))
            return;

        _sprite.CopySprite((attacher, attackerSprite), args.SpriteViewEnt.AsNullable());
    }

    private bool TryGetFaceAttacher(EntityUid viewer, [NotNullWhen(true)] out EntityUid attacher)
    {
        attacher = EntityUid.Invalid;

        if (!_inventory.TryGetSlotEntity(viewer, "mask", out var mask) || mask is not { } maskEnt)
            return false;

        if (!HasComp<FleshWormComponent>(maskEnt))
            return false;

        attacher = maskEnt;
        return true;
    }

    private void OnGetEquipmentVisuals(Entity<FleshWormComponent> ent, ref GetEquipmentVisualsEvent args)
    {
        if (args.Slot != "mask" || args.Layers.Count > 0)
            return;

        if (!TryComp(args.Equipee, out InventoryComponent? inventory))
            return;

        if (!TryBuildFallbackLayer(ent, inventory.SpeciesId, out var layer))
            return;

        args.Layers.Add(("flesh-worm-mask", layer));
    }

    private bool TryBuildFallbackLayer(
        EntityUid uid,
        string? speciesId,
        [NotNullWhen(true)] out PrototypeLayerData layer)
    {
        layer = default!;

        if (!TryResolveRsi(uid, out var rsi))
            return false;

        var clothing = CompOrNull<ClothingComponent>(uid);
        if (!TryPickState(rsi, BuildStateCandidates(clothing), speciesId, uid, out var state))
            return false;

        layer = new PrototypeLayerData
        {
            RsiPath = rsi.Path.ToString(),
            State = state,
            Scale = clothing?.Scale ?? Vector2.One,
        };

        return true;
    }

    private bool TryResolveRsi(EntityUid uid, [NotNullWhen(true)] out RSI? rsi)
    {
        rsi = null;

        if (TryComp<ClothingComponent>(uid, out var clothing) && clothing.RsiPath != null)
        {
            rsi = _cache.GetResource<RSIResource>(SpriteSpecifierSerializer.TextureRoot / clothing.RsiPath).RSI;
            return true;
        }

        if (TryComp<SpriteComponent>(uid, out var sprite) && sprite.BaseRSI != null)
        {
            rsi = sprite.BaseRSI;
            return true;
        }

        return false;
    }

    private static List<string> BuildStateCandidates(ClothingComponent? clothing)
    {
        var candidates = new List<string>();

        if (clothing?.EquippedState != null)
            candidates.Add(clothing.EquippedState);

        if (!string.IsNullOrEmpty(clothing?.EquippedPrefix))
            candidates.Add($"{clothing.EquippedPrefix}-equipped-MASK");

        candidates.Add("equipped-MASK");
        candidates.Add("equipped-HELMET");
        candidates.Add("worm-equipped-MASK");
        candidates.Add("icon");
        return candidates;
    }

    private bool TryPickState(
        RSI rsi,
        List<string> candidates,
        string? speciesId,
        EntityUid uid,
        [NotNullWhen(true)] out string? state)
    {
        foreach (var candidate in candidates)
        {
            if (speciesId != null && rsi.TryGetState($"{candidate}-{speciesId}", out _))
            {
                state = $"{candidate}-{speciesId}";
                return true;
            }

            if (rsi.TryGetState(candidate, out _))
            {
                state = candidate;
                return true;
            }
        }

        if (TryComp<SpriteComponent>(uid, out var sprite))
        {
            foreach (var spriteLayer in sprite.AllLayers)
            {
                if (spriteLayer.RsiState.Name is { } layerState)
                {
                    state = layerState;
                    return true;
                }
            }
        }

        state = null;
        return false;
    }
}
