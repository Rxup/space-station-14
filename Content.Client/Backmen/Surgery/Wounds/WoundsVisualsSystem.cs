using System.Linq;
using Content.Shared.Backmen.Surgery.Wounds;
using Content.Shared.Backmen.Surgery.Wounds.Components;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Humanoid;
using Robust.Client.GameObjects;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Client.Backmen.Surgery.Wounds;

public sealed class WoundsVisualsSystem : EntitySystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedBodySystem _body = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    private const float AltBleedingSpriteChance = 0.4f;

    private readonly HashSet<string> _damageGroups = ["Brute", "Burn", "Bleeding"];

    private readonly Dictionary<WoundableSeverity, FixedPoint2> _visualizerThresholds = new()
    {
        { WoundableSeverity.Moderate, 25 },
        { WoundableSeverity.Severe, 50 },
        { WoundableSeverity.Critical, 80 },
    };

    private readonly Dictionary<BleedingSeverity, FixedPoint2> _bleedingThresholds = new()
    {
        { BleedingSeverity.Minor, 0.05 },
        { BleedingSeverity.Severe, 0.30},
    };

    private readonly Dictionary<WoundSeverity, FixedPoint2> _severityPoints = new()
    {
        { WoundSeverity.Healed, 0},
        { WoundSeverity.Minor, 0.02 },
        { WoundSeverity.Moderate, 0.06 },
        { WoundSeverity.Severe, 0.08 },
        { WoundSeverity.Critical, 0.10 },
        { WoundSeverity.Loss, 0.20},
    };

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WoundableVisualsComponent, ComponentInit>(InitializeEntity);
        SubscribeLocalEvent<BodyPartComponent, AppearanceChangeEvent>((uid, comp, _) => OnAppearanceChange(uid, comp));
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
                    sprite,
                    $"{layer}_{group}_{_visualizerThresholds[WoundableSeverity.Moderate]}",
                    $"{layer}{group}",
                    index);
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
    }

    private void OnAppearanceChange(EntityUid uid, BodyPartComponent bodyPart)
    {
        if (bodyPart.Body == null ||
            !TryComp<SpriteComponent>(bodyPart.Body.Value, out var spriteComponent))
            return;

        UpdateWoundableVisuals(uid, bodyPart, spriteComponent);
    }

    private void AddDamageLayerToSprite(SpriteComponent spriteComponent, string sprite, string state, string mapKey, int? index = null)
    {
        var newLayer = spriteComponent.AddLayer(
            new SpriteSpecifier.Rsi(
                new ResPath(sprite),
                state
            ),
            index);
        spriteComponent.LayerMapSet(mapKey, newLayer);
        spriteComponent.LayerSetVisible(newLayer, false);
    }

    private void UpdateWoundableVisuals(EntityUid uid, BodyPartComponent bodyPart, SpriteComponent sprite)
    {
        if (!_appearance.TryGetData<WoundsVisualizerGroupData>(uid, WoundableVisualizerKeys.Wounds, out var data)
            || !_appearance.TryGetData<WoundableSeverity>(uid, WoundableVisualizerKeys.Severity, out var severity))
            return;

        var layer = bodyPart.ToHumanoidLayers();
        if (layer == null)
            return;

        if (UpdateLostWoundableVisuals(layer.Value, sprite, severity))
            return;

        var damageGroups = new Dictionary<string, FixedPoint2>();
        foreach (var wound in data.WoundsList)
        {
            var woundDmgGroup = Comp<WoundComponent>(GetEntity(wound)).DamageGroup;
            if (woundDmgGroup == null)
                continue;

            if (damageGroups.TryGetValue(woundDmgGroup, out var value))
            {
                damageGroups[woundDmgGroup] = value + Comp<WoundComponent>(GetEntity(wound)).WoundSeverityPoint;
            }
            else
            {
                damageGroups.Add(woundDmgGroup, Comp<WoundComponent>(GetEntity(wound)).WoundSeverityPoint);
            }
        }

        foreach (var damageGroup in damageGroups)
        {
            sprite.LayerMapTryGet($"{layer}{damageGroup.Key}", out var damageLayer);

            UpdateDamageLayerState(sprite, damageLayer, $"{layer}_{damageGroup.Key}", GetThreshold(damageGroup.Value));
        }

        UpdateBleeding(uid, layer.Value, bodyPart, sprite);
    }

    private bool UpdateLostWoundableVisuals(HumanoidVisualLayers key, SpriteComponent sprite, WoundableSeverity? severity = null)
    {
        sprite.LayerMapTryGet(key, out var layer);
        if (severity != WoundableSeverity.Loss)
        {
            sprite.LayerSetVisible(layer, true);

            return false;
        }

        foreach (var damageGroup in _damageGroups)
        {
            sprite.LayerMapTryGet($"{key}{damageGroup}", out var damageLayer);

            UpdateDamageLayerState(sprite, damageLayer, $"{key}_{damageGroup}", -100);
        }

        if (key == HumanoidVisualLayers.Head)
        {
            sprite.LayerMapTryGet(HumanoidVisualLayers.Eyes, out var eyesLayer);
            sprite.LayerSetVisible(eyesLayer, false);

            sprite.LayerMapTryGet(HumanoidVisualLayers.Hair, out var hairLayer);
            sprite.LayerSetVisible(hairLayer, false);
        }

        sprite.LayerSetVisible(layer, false);
        return true;
    }

    private void UpdateBleeding(EntityUid uid, HumanoidVisualLayers layer, BodyPartComponent bodyPart, SpriteComponent sprite)
    {
        if (bodyPart.PartType is BodyPartType.Foot or BodyPartType.Hand)
        {
            if (!_body.TryGetParentBodyPart(uid, out var parentUid, out _))
                return;

            if (!_appearance.TryGetData<WoundsVisualizerGroupData>(parentUid.Value, WoundableVisualizerKeys.Wounds, out var parentData)
                || !_appearance.TryGetData<WoundsVisualizerGroupData>(uid, WoundableVisualizerKeys.Wounds, out var data))
                return;

            var woundsToProcess = parentData.WoundsList.ToList();
            woundsToProcess.AddRange(data.WoundsList);

            var totalDamage =
                woundsToProcess.Select(wound => new { wound, woundComp = Comp<WoundComponent>(GetEntity(wound)) })
                    .Where(t => t.woundComp.CanBleed)
                    .Select(t => t.wound)
                    .Aggregate((FixedPoint2) 0,
                        (current, wound) => current + Comp<WoundComponent>(GetEntity(wound)).WoundSeverityPoint
                * _severityPoints[Comp<WoundComponent>(GetEntity(wound)).WoundSeverity]);

            var symmetry = bodyPart.Symmetry == BodyPartSymmetry.Left ? "L" : "R";
            var partType = bodyPart.PartType == BodyPartType.Foot ? "Leg" : "Arm";

            var part = symmetry + partType;

            sprite.LayerMapTryGet($"{part}Bleeding", out var parentBleedingLayer);

            UpdateBleedingLayerState(
                sprite,
                parentBleedingLayer,
                part,
                GetBleedingThreshold(totalDamage));
        }
        else
        {
            if (!_appearance.TryGetData<WoundsVisualizerGroupData>(uid, WoundableVisualizerKeys.Wounds, out var data))
                return;

            var totalDamage =
                data.WoundsList.Aggregate((FixedPoint2) 0, (current, wound) => current + Comp<WoundComponent>(GetEntity(wound)).WoundSeverityPoint);

            sprite.LayerMapTryGet($"{layer}Bleeding", out var bleedingLayer);

            UpdateBleedingLayerState(sprite,
                bleedingLayer,
                layer.ToString(),
                GetBleedingThreshold(totalDamage));
        }
    }

    private FixedPoint2 GetThreshold(FixedPoint2 threshold)
    {
        var nearestSeverity = (FixedPoint2) 0;

        foreach (var (_, value) in _visualizerThresholds.OrderByDescending(kv => kv.Value))
        {
            if (threshold < value)
                continue;

            nearestSeverity = value;
            break;
        }

        return nearestSeverity;
    }

    private BleedingSeverity GetBleedingThreshold(FixedPoint2 threshold)
    {
        var nearestSeverity = BleedingSeverity.Minor;

        foreach (var (key, value) in _bleedingThresholds.OrderByDescending(kv => kv.Value))
        {
            if (threshold < value)
                continue;

            nearestSeverity = key;
            break;
        }

        return nearestSeverity;
    }

    private void UpdateBleedingLayerState(SpriteComponent spriteComponent, int spriteLayer, string statePrefix, BleedingSeverity threshold)
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
