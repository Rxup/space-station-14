/// Maded by Gorox. Discord - smeshinka112. Сделано на основе: https://github.com/space-wizards/space-station-14/pull/20443
using Content.Client.Items.Systems;
using Content.Shared.Clothing;
using Content.Shared.Backmen.XenoPotionEffected.Components;
using Robust.Client.GameObjects;

namespace Content.Client.Backmen.XenoPotionEffected;

public sealed class XenoPotionEffectedSystem : VisualizerSystem<XenoPotionEffectedComponent>
{
    [Dependency] private readonly ItemSystem _item = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<XenoPotionEffectedComponent, AfterAutoHandleStateEvent>(OnAfterState);
        SubscribeLocalEvent<XenoPotionEffectedComponent, EquipmentVisualsUpdatedEvent>(OnVisualsUpdated);
    }

    private void OnAfterState(EntityUid uid, XenoPotionEffectedComponent component, ref AfterAutoHandleStateEvent @event)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite))
        {
            return;
        }
        sprite.Color = component.Color;
        _item.VisualsChanged(uid);
    }

    private void OnVisualsUpdated(EntityUid uid, XenoPotionEffectedComponent component, EquipmentVisualsUpdatedEvent @event)
    {
        if (!TryComp<SpriteComponent>(@event.Equipee, out var sprite))
        {
            return;
        }
        foreach (var layer in @event.RevealedLayers)
        {
            sprite.LayerSetColor(layer, component.Color);
        }
    }
}