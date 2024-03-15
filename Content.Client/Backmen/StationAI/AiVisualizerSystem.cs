using System.Linq;
using Content.Client.Power;
using Content.Shared.Backmen.StationAI;
using Content.Shared.Backmen.StationAI.UI;
using Content.Shared.Power;
using Robust.Client.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager;

namespace Content.Client.Backmen.StationAI;

public sealed class AiVisualizerSystem : VisualizerSystem<StationAIComponent>
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly ISerializationManager _serialization = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StationAIComponent, AfterAutoHandleStateEvent>(OnUpdate);
    }

    protected override void OnAppearanceChange(EntityUid uid, StationAIComponent component, ref AppearanceChangeEvent args)
    {
        if (HasComp<AIEyeComponent>(uid))
        {
            base.OnAppearanceChange(uid, component, ref args);
            return;
        }

        UpdateAppearance(uid, component, args.Component, args.Sprite);
    }

    private void OnUpdate(Entity<StationAIComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (HasComp<AIEyeComponent>(ent))
        {
            return;
        }
        if (
            !ent.Comp.Layers.ContainsKey(ent.Comp.SelectedLayer) ||
            !TryComp<MetaDataComponent>(ent, out var metaDataComponent) ||
            metaDataComponent.EntityPrototype == null ||
            !_prototypeManager.TryIndex<EntityPrototype>(metaDataComponent.EntityPrototype.ID, out var proto) ||
            !proto.TryGetComponent<SpriteComponent>(out var spriteProtoComponent)
            )
        {
            return;
        }

        var spriteComponent = (SpriteComponent) _serialization.CreateCopy(spriteProtoComponent, notNullableOverride: true);

        var layers = ent.Comp.Layers[ent.Comp.SelectedLayer];

        foreach (var layer in layers)
        {
            spriteComponent.AddLayer(layer);
        }

        EntityManager.AddComponent(ent, spriteComponent, true);

        UpdateAppearance(ent, ent, sprite: spriteComponent);
    }

    private void UpdateAppearance(EntityUid id, StationAIComponent sign, AppearanceComponent? appearance = null,
        SpriteComponent? sprite = null)
    {
        if (!Resolve(id, ref appearance, ref sprite))
            return;

        AppearanceSystem.TryGetData<bool>(id, PowerDeviceVisuals.Powered, out var powered, appearance);
        AppearanceSystem.TryGetData<bool>(id, AiVisuals.Dead, out var dead, appearance);
        AppearanceSystem.TryGetData<bool>(id, AiVisuals.InEye, out var inEye, appearance);

        if (sprite.LayerMapTryGet(AiVisualLayers.NotInEye, out var eyeLayer))
        {
            sprite.LayerSetVisible(eyeLayer, powered && !inEye && !dead);
        }

        if (sprite.LayerMapTryGet(AiVisualLayers.Dead, out var deadLayer))
        {
            sprite.LayerSetVisible(deadLayer, powered && dead);
        }

        if (sprite.LayerMapTryGet(PowerDeviceVisualLayers.Powered, out var poweredLayer))
        {
            sprite.LayerSetVisible(poweredLayer, powered && !dead);
        }
    }
}
