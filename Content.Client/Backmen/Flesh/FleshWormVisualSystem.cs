using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Content.Client.Clothing;
using Content.Shared.Backmen.Flesh;
using Content.Shared.Clothing;
using Content.Shared.Clothing.Components;
using Content.Shared.Clothing.EntitySystems;
using Content.Shared.Inventory;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Serialization.TypeSerializers.Implementations;
using Robust.Shared.Utility;

namespace Content.Client.Backmen.Flesh;

/// <summary>
/// Fallback mask visuals for face-attached flesh worms and headcrabs when clothing RSI lacks equipped states.
/// </summary>
public sealed partial class FleshWormVisualSystem : EntitySystem
{
    [Dependency] private IResourceCache _cache = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FleshWormComponent, GetEquipmentVisualsEvent>(OnGetEquipmentVisuals,
            after: [typeof(ClientClothingSystem)]);
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
