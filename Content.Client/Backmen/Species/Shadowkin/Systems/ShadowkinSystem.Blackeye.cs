using Content.Shared.Backmen.Species.Shadowkin.Events;
using Content.Shared.Backmen.Species.Shadowkin.Components;
using Robust.Client.GameObjects;
using Content.Shared.Humanoid;

namespace Content.Client.Backmen.Species.Shadowkin.Systems;

public sealed class ShadowkinBlackeyeSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<ShadowkinBlackeyeEvent>(OnBlackeye);

        SubscribeLocalEvent<ShadowkinComponent, ComponentInit>(OnInit);
    }

    private void OnBlackeye(ShadowkinBlackeyeEvent ev)
    {
        SetColor( GetEntity(ev.Uid), Color.Black);
    }


    private void OnInit(EntityUid uid, ShadowkinComponent component, ComponentInit args)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite) ||
            !sprite.LayerMapTryGet(HumanoidVisualLayers.Eyes, out var index) ||
            !sprite.TryGetLayer(index, out var layer))
            return;

        // Blackeye if none of the RGB values are greater than 75
        if (layer.Color.R * 255 < 75 && layer.Color.G * 255 < 75 && layer.Color.B * 255 < 75)
        {
            RaiseNetworkEvent(new ShadowkinBlackeyeEvent(GetNetEntity(uid), false));
        }
    }


    private void SetColor(EntityUid uid, Color color)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite) ||
            !sprite.LayerMapTryGet(HumanoidVisualLayers.Eyes, out var index) ||
            !sprite.TryGetLayer(index, out var layer))
            return;

        sprite.LayerSetColor(index, color);
    }
}
