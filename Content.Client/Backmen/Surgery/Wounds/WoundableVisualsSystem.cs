using System.Linq;
using Content.Shared.Backmen.Surgery.Body.Events;
using Content.Shared.Backmen.Surgery.Wounds;
using Content.Shared.Backmen.Surgery.Wounds.Components;
using Content.Shared.Backmen.Surgery.Wounds.Systems;
using Content.Shared.Backmen.Targeting;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Humanoid;
using Robust.Client.GameObjects;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Client.Backmen.Surgery.Wounds;

public sealed class WoundableVisualsSystem : VisualizerSystem<WoundableVisualsComponent>
{
    [Dependency] private readonly SharedBodySystem _body = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly WoundSystem _wounds = default!;

    private const float AltBleedingSpriteChance = 0.15f;
    private readonly HashSet<string> _damageGroups = ["Brute", "Burn", "Bleeding"];

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WoundableVisualsComponent, ComponentInit>(InitializeEntity);
        SubscribeLocalEvent<WoundableComponent, BodyPartRemovedEvent>(WoundableRemoved);
    }

    private void InitializeEntity(EntityUid uid, WoundableVisualsComponent component, ComponentInit args)
    {
        if (!TryComp(uid, out SpriteComponent? spriteComponent))
            return;

        if (component.TargetLayers is not { Count: > 0 })
            return;

        foreach (var layer in component.TargetLayers.Where(layer => spriteComponent.LayerMapTryGet(layer, out _)))
        {
            component.TargetLayerMapKeys.Add(layer);
        }

        foreach (var layer in component.TargetLayerMapKeys.Where(_ => component.DamageOverlayGroups != null))
        {
            var layerCount = spriteComponent.AllLayers.Count();
            var index = spriteComponent.LayerMapGet(layer);

            if (index + 1 != layerCount)
            {
                index += 1;
            }

            foreach (var (group, sprite) in component.DamageOverlayGroups!)
            {
                AddDamageLayerToSprite(spriteComponent,
                    sprite.Sprite,
                    $"{layer}_{group}_100",
                    $"{layer}{group}",
                    index,
                    sprite.Color);
            }
        }

        foreach (var layer in component.TargetLayerMapKeys.Where(_ => component.DamageOverlayGroups != null))
        {
            var layerCount = spriteComponent.AllLayers.Count();
            var index = spriteComponent.LayerMapGet(layer);

            if (index + 1 != layerCount)
            {
                index += 1;
            }

            if (Equals(layer, HumanoidVisualLayers.LHand)
                || Equals(layer, HumanoidVisualLayers.RHand)
                || Equals(layer, HumanoidVisualLayers.LFoot)
                || Equals(layer, HumanoidVisualLayers.RFoot))
                continue;

            AddDamageLayerToSprite(spriteComponent,
                component.BleedingOverlay,
                $"{layer}_Minor",
                $"{layer}Bleeding",
                index);
        }

        foreach (var (part, comp) in _body.GetBodyChildren(uid))
        {
            var layer = comp.ToHumanoidLayers();
            if (layer == null)
                continue;

            UpdateWoundableVisuals(part, component, layer.Value, spriteComponent);
            spriteComponent.LayerSetVisible(layer, true); // for some reason, the fucking layers sometimes just disappear. maybe preloading issue maybe
        }
    }

    private void WoundableRemoved(EntityUid uid, WoundableComponent component, BodyPartRemovedEvent args)
    {
        if (!TryComp<SpriteComponent>(uid, out var spriteComponent))
            return;

        var layer = args.Part.Comp.ToHumanoidLayers();
        if (layer == null)
            return;

        if (!TryComp<WoundableVisualsComponent>(args.Part.Comp.LastBody, out var visuals))
            return;

        foreach (var (group, sprite) in visuals.DamageOverlayGroups!)
        {
            if (!spriteComponent.LayerMapTryGet($"{layer}{group}", out _))
            {
                var newLayer = spriteComponent.AddLayer(
                    new SpriteSpecifier.Rsi(
                        new ResPath(sprite.Sprite),
                        $"{layer}_{group}_100"
                    ));
                spriteComponent.LayerMapSet($"{layer}{group}", newLayer);

                if (sprite.Color != null)
                    spriteComponent.LayerSetColor(newLayer, Color.FromHex(sprite.Color));
                spriteComponent.LayerSetVisible(newLayer, false);
            }
        }

        if (args.Part.Comp.PartType is not BodyPartType.Hand and not BodyPartType.Foot)
        {
            if (!spriteComponent.LayerMapTryGet($"{layer}Bleeding", out _))
            {
                var bleedLayer = spriteComponent.AddLayer(
                    new SpriteSpecifier.Rsi(
                        new ResPath(visuals.BleedingOverlay),
                        $"{layer}_Severe"
                    ));
                spriteComponent.LayerMapSet($"{layer}Bleeding", bleedLayer);
                spriteComponent.LayerSetVisible(bleedLayer, false);
            }
        }

        UpdateWoundableVisuals(args.Part, visuals, layer.Value, spriteComponent);
    }

    protected override void OnAppearanceChange(EntityUid body, WoundableVisualsComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        foreach (var (bodyPart, severity) in _wounds.GetWoundableStatesOnBody(body))
        {
            var layer = SharedTargetingSystem.ToVisualLayers(bodyPart);
            args.Sprite.LayerMapTryGet(layer, out var key);

            if (severity == WoundableSeverity.Loss)
            {
                args.Sprite.LayerSetVisible(key, false);
                foreach (var damageGroup in _damageGroups)
                {
                    if (args.Sprite.LayerMapTryGet($"{layer}{damageGroup}", out var damageLayer))
                    {
                        args.Sprite.LayerSetVisible(damageLayer, false);
                    }
                }

                if (layer == HumanoidVisualLayers.Head)
                {
                    if (args.Sprite.LayerMapTryGet(HumanoidVisualLayers.Eyes, out var eyesLayer))
                    {
                        args.Sprite.LayerSetVisible(eyesLayer, false);
                    }

                    if (args.Sprite.LayerMapTryGet(HumanoidVisualLayers.Hair, out var hairLayer))
                    {
                        args.Sprite.LayerSetVisible(hairLayer, false);
                    }
                }
            }
            else
            {
                args.Sprite.LayerSetVisible(key, true);

                var (type, symmetry) = _body.ConvertTargetBodyPart(bodyPart);
                var parts = _body.GetBodyChildrenOfType(body, type, symmetry: symmetry);

                foreach (var (uid, _) in parts)
                {
                    UpdateWoundableVisuals(uid, component, layer, args.Sprite);
                }
            }
        }
    }

    private void AddDamageLayerToSprite(SpriteComponent spriteComponent, string sprite, string state, string mapKey, int? index = null, string? color = null)
    {
        var newLayer = spriteComponent.AddLayer(
            new SpriteSpecifier.Rsi(
                new ResPath(sprite),
                state
            ),
            index);
        spriteComponent.LayerMapSet(mapKey, newLayer);
        if (color != null)
            spriteComponent.LayerSetColor(newLayer, Color.FromHex(color));
        spriteComponent.LayerSetVisible(newLayer, false);
    }

    private void UpdateWoundableVisuals(EntityUid uid, WoundableVisualsComponent visuals, HumanoidVisualLayers layer, SpriteComponent sprite)
    {
        var woundable = Comp<WoundableComponent>(uid);

        var damagePerGroup = new Dictionary<string, FixedPoint2>();
        foreach (var wound in woundable.Wounds!.ContainedEntities)
        {
            var comp = Comp<WoundComponent>(wound);
            if (comp.DamageGroup == null)
                continue;

            if (!damagePerGroup.TryAdd(comp.DamageGroup, comp.WoundSeverityPoint))
            {
                damagePerGroup[comp.DamageGroup] += comp.WoundSeverityPoint;
            }
        }

        foreach (var (type, damage) in damagePerGroup)
        {
            sprite.LayerMapTryGet($"{layer}{type}", out var damageLayer);

            UpdateDamageLayerState(sprite, damageLayer, $"{layer}_{type}", GetThreshold(damage, visuals));
        }

        UpdateBleeding(uid, visuals, layer, sprite);
    }

    private void UpdateBleeding(EntityUid uid, WoundableVisualsComponent comp, HumanoidVisualLayers layer, SpriteComponent sprite)
    {
        if (!TryComp<BodyPartComponent>(uid, out var bodyPart))
            return;

        if (bodyPart.PartType is BodyPartType.Foot or BodyPartType.Hand)
        {
            if (!_body.TryGetParentBodyPart(uid, out var parentUid, out _))
                return;

            var woundList = new List<EntityUid>();
            woundList.AddRange(Comp<WoundableComponent>(uid).Wounds!.ContainedEntities);
            woundList.AddRange(Comp<WoundableComponent>(parentUid.Value).Wounds!.ContainedEntities);

            var totalDamage = woundList.Aggregate((FixedPoint2) 0, (current, wound) => current + Comp<WoundComponent>(wound).WoundSeverityPoint);

            var symmetry = bodyPart.Symmetry == BodyPartSymmetry.Left ? "L" : "R";
            var partType = bodyPart.PartType == BodyPartType.Foot ? "Leg" : "Arm";

            var part = symmetry + partType;

            sprite.LayerMapTryGet($"{part}Bleeding", out var parentBleedingLayer);

            UpdateBleedingLayerState(
                sprite,
                parentBleedingLayer,
                part,
                totalDamage,
                GetBleedingThreshold(totalDamage, comp));
        }
        else
        {
            var totalDamage =
                Comp<WoundableComponent>(uid).Wounds!.ContainedEntities.Aggregate((FixedPoint2) 0,
                    (current, wound) => (int)(current + Comp<WoundComponent>(wound).WoundSeverityPoint));

            sprite.LayerMapTryGet($"{layer}Bleeding", out var bleedingLayer);

            UpdateBleedingLayerState(sprite,
                bleedingLayer,
                layer.ToString(),
                totalDamage,
                GetBleedingThreshold(totalDamage, comp));
        }
    }

    private FixedPoint2 GetThreshold(FixedPoint2 threshold, WoundableVisualsComponent comp)
    {
        var nearestSeverity = (FixedPoint2) 0;

        foreach (var value in comp.Thresholds.OrderByDescending(kv => kv.Value))
        {
            if (threshold < value)
                continue;

            nearestSeverity = value;
            break;
        }

        return nearestSeverity;
    }

    private BleedingSeverity GetBleedingThreshold(FixedPoint2 threshold, WoundableVisualsComponent comp)
    {
        var nearestSeverity = BleedingSeverity.Minor;

        foreach (var (key, value) in comp.BleedingThresholds.OrderByDescending(kv => kv.Value))
        {
            if (threshold < value)
                continue;

            nearestSeverity = key;
            break;
        }

        return nearestSeverity;
    }

    private void UpdateBleedingLayerState(SpriteComponent spriteComponent, int spriteLayer, string statePrefix, FixedPoint2 damage, BleedingSeverity threshold)
    {
        if (damage <= 0)
        {
            spriteComponent.LayerSetVisible(spriteLayer, false);
        }
        else
        {
            if (!spriteComponent[spriteLayer].Visible)
            {
                spriteComponent.LayerSetVisible(spriteLayer, true);
            }

            if (_random.Prob(AltBleedingSpriteChance))
            {
                var rsi = spriteComponent.LayerGetActualRSI(spriteLayer);

                if (rsi != null && rsi.TryGetState($"{statePrefix}_{threshold}_alt", out _))
                {
                    spriteComponent.LayerSetState(spriteLayer, $"{statePrefix}_{threshold}_alt");
                }
            }
            else
            {
                spriteComponent.LayerSetState(spriteLayer, $"{statePrefix}_{threshold}");
            }
        }
    }

    private void UpdateDamageLayerState(SpriteComponent spriteComponent, int spriteLayer, string statePrefix, FixedPoint2 threshold)
    {
        if (threshold <= 0)
        {
            spriteComponent.LayerSetVisible(spriteLayer, false);
        }
        else
        {
            if (!spriteComponent[spriteLayer].Visible)
            {
                spriteComponent.LayerSetVisible(spriteLayer, true);
            }
            spriteComponent.LayerSetState(spriteLayer, $"{statePrefix}_{threshold}");
        }
    }
}
