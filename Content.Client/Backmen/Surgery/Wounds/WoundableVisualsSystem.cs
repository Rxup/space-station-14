﻿using System.Linq;
using Content.Shared.Backmen.Surgery.Body.Events;
using Content.Shared.Backmen.Surgery.Traumas.Components;
using Content.Shared.Backmen.Surgery.Wounds;
using Content.Shared.Backmen.Surgery.Wounds.Components;
using Content.Shared.Backmen.Surgery.Wounds.Systems;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Humanoid;
using Robust.Client.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Client.Backmen.Surgery.Wounds;

public sealed class WoundableVisualsSystem : VisualizerSystem<WoundableVisualsComponent>
{
    [Dependency] private readonly SharedBodySystem _body = default!;

    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IPrototypeManager _protoMan = default!;

    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;

    private const float AltBleedingSpriteChance = 0.15f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WoundableVisualsComponent, ComponentInit>(InitializeEntity, after:[typeof(WoundSystem)]);
        SubscribeLocalEvent<WoundableVisualsComponent, BodyPartRemovedEvent>(WoundableRemoved);
        SubscribeLocalEvent<WoundableVisualsComponent, BodyPartAddedEvent>(WoundableConnected);
    }

    private void InitializeEntity(EntityUid uid, WoundableVisualsComponent component, ComponentInit args)
    {
        if (!TryComp(uid, out SpriteComponent? partSprite))
            return;

        foreach (var (group, sprite) in component.DamageOverlayGroups!)
        {
            AddDamageLayerToSprite(partSprite,
                sprite.Sprite,
                $"{component.OccupiedLayer}_{group}_100",
                $"{component.OccupiedLayer}{group}",
                sprite.Color);
        }

        if (Equals(component.OccupiedLayer, HumanoidVisualLayers.LHand)
            || Equals(component.OccupiedLayer, HumanoidVisualLayers.RHand)
            || Equals(component.OccupiedLayer, HumanoidVisualLayers.LFoot)
            || Equals(component.OccupiedLayer, HumanoidVisualLayers.RFoot))
            return;

        AddDamageLayerToSprite(partSprite,
            component.BleedingOverlay,
            $"{component.OccupiedLayer}_Minor",
            $"{component.OccupiedLayer}Bleeding");
    }

    private void WoundableConnected(EntityUid uid, WoundableVisualsComponent component, BodyPartAddedEvent args)
    {
        var bodyPart = args.Part.Comp;
        if (!bodyPart.Body.HasValue || !TryComp(bodyPart.Body.Value, out SpriteComponent? bodySprite))
            return;

        foreach (var (group, sprite) in component.DamageOverlayGroups!)
        {
            if (!bodySprite.LayerMapTryGet($"{component.OccupiedLayer}{group}", out _))
            {
                AddDamageLayerToSprite(bodySprite,
                    sprite.Sprite,
                    $"{component.OccupiedLayer}_{group}_100",
                    $"{component.OccupiedLayer}{group}",
                    sprite.Color);
            }
        }

        if (Equals(component.OccupiedLayer, HumanoidVisualLayers.LHand)
            || Equals(component.OccupiedLayer, HumanoidVisualLayers.RHand)
            || Equals(component.OccupiedLayer, HumanoidVisualLayers.LFoot)
            || Equals(component.OccupiedLayer, HumanoidVisualLayers.RFoot))
            return;

        if (!bodySprite.LayerMapTryGet($"{component.OccupiedLayer}Bleeding", out _))
        {
            AddDamageLayerToSprite(bodySprite,
                component.BleedingOverlay,
                $"{component.OccupiedLayer}_Minor",
                $"{component.OccupiedLayer}Bleeding");
        }

        UpdateWoundableVisuals(uid, component, bodySprite);
    }

    private void WoundableRemoved(EntityUid uid, WoundableVisualsComponent component, BodyPartRemovedEvent args)
    {
        var body = args.Part.Comp.Body;
        if (!TryComp(body, out SpriteComponent? bodySprite))
            return;

        foreach (var (group, _) in component.DamageOverlayGroups!)
        {
            if (!bodySprite.LayerMapTryGet($"{component.OccupiedLayer}{group}", out var layer))
                continue;

            bodySprite.LayerSetVisible(layer, false);
            bodySprite.LayerMapRemove(layer);
        }

        if (bodySprite.LayerMapTryGet($"{component.OccupiedLayer}Bleeding", out var bleeds))
        {
            bodySprite.LayerSetVisible(bleeds, false);
            bodySprite.LayerMapRemove(bleeds);
        }

        foreach (var part in _body.GetBodyPartChildren(uid))
        {
            if (!TryComp<WoundableVisualsComponent>(part.Id, out var woundableVisuals))
                continue;

            foreach (var (group, _) in component.DamageOverlayGroups!)
            {
                if (!bodySprite.LayerMapTryGet($"{component.OccupiedLayer}{group}", out var layer))
                    continue;

                bodySprite.LayerSetVisible(layer, false);
                bodySprite.LayerMapRemove(layer);
            }

            if (bodySprite.LayerMapTryGet($"{component.OccupiedLayer}Bleeding", out var childBleeds))
            {
                bodySprite.LayerSetVisible(childBleeds, false);
                bodySprite.LayerMapRemove(childBleeds);
            }

            if (TryComp(uid, out SpriteComponent? pieceSprite))
                UpdateWoundableVisuals(part.Id, woundableVisuals, pieceSprite);
        }

        if (TryComp(uid, out SpriteComponent? partSprite))
            UpdateWoundableVisuals(uid, component, partSprite);
    }

    protected override void OnAppearanceChange(EntityUid uid, WoundableVisualsComponent component, ref AppearanceChangeEvent args)
    {
        var bodyPart = Comp<BodyPartComponent>(uid);
        if (!bodyPart.Body.HasValue)
        {
            if (TryComp(uid, out SpriteComponent? partSprite))
                UpdateWoundableVisuals(uid, component, partSprite);
            return;
        }

        if (TryComp(bodyPart.Body.Value, out SpriteComponent? bodySprite))
            UpdateWoundableVisuals(uid, component, bodySprite);
    }

    private void AddDamageLayerToSprite(SpriteComponent spriteComponent, string sprite, string state, string mapKey, string? color = null)
    {
        var newLayer = spriteComponent.AddLayer(
            new SpriteSpecifier.Rsi(
                new ResPath(sprite),
                state
            ));
        spriteComponent.LayerMapSet(mapKey, newLayer);
        if (color != null)
            spriteComponent.LayerSetColor(newLayer, Color.FromHex(color));
        spriteComponent.LayerSetVisible(newLayer, false);
    }

    private void UpdateWoundableVisuals(EntityUid uid, WoundableVisualsComponent visuals, SpriteComponent bodySprite)
    {
        if (!_appearance.TryGetData<WoundVisualizerGroupData>(uid, WoundableVisualizerKeys.Wounds, out var wounds))
            return;

        var damagePerGroup = new Dictionary<string, FixedPoint2>();
        foreach (var comp in wounds.GroupList.Select(GetEntity).Where(ent => !TerminatingOrDeleted(ent)).Select(Comp<WoundComponent>))
        {
            if (comp.DamageGroup == null)
                continue;

            if (!damagePerGroup.TryAdd(comp.DamageGroup, comp.WoundSeverityPoint))
            {
                damagePerGroup[comp.DamageGroup] += comp.WoundSeverityPoint;
            }
        }

        if (damagePerGroup.Count == 0 && visuals.DamageOverlayGroups != null)
        {
            foreach (var damage in visuals.DamageOverlayGroups!)
            {
                bodySprite.LayerMapTryGet($"{visuals.OccupiedLayer}{damage.Key}", out var damageLayer);

                UpdateDamageLayerState(bodySprite, damageLayer, $"{visuals.OccupiedLayer}_{damage.Key}", 0);
            }
        }

        foreach (var (type, damage) in damagePerGroup)
        {
            if (!visuals.DamageOverlayGroups!.ContainsKey(type))
                continue;

            bodySprite.LayerMapTryGet($"{visuals.OccupiedLayer}{type}", out var damageLayer);

            UpdateDamageLayerState(bodySprite, damageLayer, $"{visuals.OccupiedLayer}_{type}", GetThreshold(damage, visuals));
        }

        UpdateBleeding(uid, visuals, visuals.OccupiedLayer, bodySprite);
    }

    private void UpdateBleeding(EntityUid uid, WoundableVisualsComponent comp, Enum layer, SpriteComponent sprite)
    {
        if (!TryComp<BodyPartComponent>(uid, out var bodyPart) || !bodyPart.Body.HasValue)
            return;

        if (bodyPart.PartType is BodyPartType.Foot or BodyPartType.Hand)
        {
            if (!_body.TryGetParentBodyPart(uid, out var parentUid, out _))
                return;

            if (!_appearance.TryGetData<WoundVisualizerGroupData>(uid, WoundableVisualizerKeys.Wounds, out var wounds)
                || !_appearance.TryGetData<WoundVisualizerGroupData>(parentUid.Value, WoundableVisualizerKeys.Wounds, out var parentWounds))
                return;

            var woundList = new List<EntityUid>();
            woundList.AddRange(wounds.GroupList.Select(GetEntity));
            woundList.AddRange(parentWounds.GroupList.Select(GetEntity));

            var totalBleeds = (FixedPoint2) 0;
            foreach (var wound in woundList)
            {
                if (TryComp<BleedInflicterComponent>(wound, out var bleeds))
                    totalBleeds += bleeds.BleedingAmount;
            }

            var symmetry = bodyPart.Symmetry == BodyPartSymmetry.Left ? "L" : "R";
            var partType = bodyPart.PartType == BodyPartType.Foot ? "Leg" : "Arm";

            var part = symmetry + partType;

            sprite.LayerMapTryGet($"{part}Bleeding", out var parentBleedingLayer);

            var color = GetBleedsColor(bodyPart.Body.Value);
            sprite.LayerSetColor(parentBleedingLayer, color);

            UpdateBleedingLayerState(
                sprite,
                parentBleedingLayer,
                part,
                totalBleeds,
                GetBleedingThreshold(totalBleeds, comp));
        }
        else
        {
            if (!_appearance.TryGetData<WoundVisualizerGroupData>(uid, WoundableVisualizerKeys.Wounds, out var wounds))
                return;

            var totalBleeds = (FixedPoint2) 0;
            foreach (var wound in wounds.GroupList.Select(GetEntity))
            {
                if (TryComp<BleedInflicterComponent>(wound, out var bleeds))
                    totalBleeds += bleeds.BleedingAmount;
            }

            sprite.LayerMapTryGet($"{layer}Bleeding", out var bleedingLayer);

            var color = GetBleedsColor(bodyPart.Body.Value);
            sprite.LayerSetColor(bleedingLayer, color);

            UpdateBleedingLayerState(sprite,
                bleedingLayer,
                layer.ToString(),
                totalBleeds,
                GetBleedingThreshold(totalBleeds, comp));
        }
    }

    private Color GetBleedsColor(EntityUid body)
    {
        // return dark red.. If for some reason there is no blood in this entity.
        if (!TryComp<BloodstreamComponent>(body, out var bloodstream))
            return Color.DarkRed;

        return _protoMan.Index(bloodstream.BloodReagent).SubstanceColor;
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
                var rsi = spriteComponent.LayerGetActualRSI(spriteLayer);

                // ... for some reason?
                if (rsi != null && rsi.TryGetState($"{statePrefix}_{threshold}", out _))
                {
                    spriteComponent.LayerSetState(spriteLayer, $"{statePrefix}_{threshold}");
                }
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
