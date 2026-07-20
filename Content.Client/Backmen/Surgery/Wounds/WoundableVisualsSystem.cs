using System.Linq;
using Content.Shared.Backmen.Surgery.Body.Events;
using Content.Shared.Backmen.Surgery.Traumas.Components;
using Content.Shared.Backmen.Surgery.Wounds;
using Content.Shared.Backmen.Surgery.Wounds.Systems;
using Content.Shared.Body;
using Content.Shared.Body.Part;
using Content.Shared.Backmen.Body.Systems;
using Content.Shared.FixedPoint;
using Robust.Client.GameObjects;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Client.Backmen.Surgery.Wounds;

public sealed partial class WoundableVisualsSystem : VisualizerSystem<WoundableVisualsComponent>
{
    [Dependency] private BkmBodySharedSystem _body = default!;
    [Dependency] private WoundSystem _wound = default!;

    [Dependency] private SpriteSystem _sprite = default!;

    [Dependency] private IRobustRandom _random = default!;

    private const float AltBleedingSpriteChance = 0.15f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WoundableVisualsComponent, ComponentInit>(InitializeEntity, after: [typeof(WoundSystem)]);
        SubscribeLocalEvent<WoundableVisualsComponent, ComponentStartup>(StartupEntity, after: [typeof(WoundSystem)]);

        SubscribeLocalEvent<BodyComponent, BodyPartRemovedEvent>(WoundableRemoved);
        SubscribeLocalEvent<BodyComponent, BodyPartAddedEvent>(WoundableConnected);

        SubscribeLocalEvent<WoundableVisualsComponent, WoundableIntegrityChangedEvent>(OnWoundableIntegrityChanged);
    }

    private void InitializeEntity(EntityUid uid, WoundableVisualsComponent component, ref ComponentInit args)
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

        if (component.BleedingOverlay != null)
        {
            AddDamageLayerToSprite(partSprite,
                component.BleedingOverlay,
                $"{component.OccupiedLayer}_Minor",
                $"{component.OccupiedLayer}Bleeding");
        }
    }

    private void StartupEntity(EntityUid uid, WoundableVisualsComponent component, ref ComponentStartup args)
    {
        if (TryComp(uid, out SpriteComponent? partSprite))
            UpdateWoundableVisuals(uid, component, partSprite);

        if (!TryGetWoundableBody(uid, out var bodyUid))
            return;

        if (!TryComp(bodyUid, out SpriteComponent? bodySprite))
            return;

        EnsureDamageLayersOnSprite(component, bodySprite);
        UpdateWoundableVisuals(uid, component, bodySprite);
    }

    private void WoundableConnected(EntityUid uid, BodyComponent component, ref BodyPartAddedEvent args)
    {
        //if (!component.ComplexBody)
        //    return;

        if (!TryComp(uid, out SpriteComponent? bodySprite)
            || !TryComp(args.Part, out WoundableVisualsComponent? visuals))
            return;

        EnsureDamageLayersOnSprite(visuals, bodySprite);
    }

    // TODO: redo this
    private void WoundableRemoved(EntityUid uid, BodyComponent component, ref BodyPartRemovedEvent args)
    {
        // TODO: derive bodies by complexness
        //if (!component.ComplexBody)
        //    return;

        if (!TryComp(uid, out SpriteComponent? bodySprite))
            return;

        var removedPart = args.Part;

        Entity<SpriteComponent?> bodyEnt = (uid, bodySprite);
        foreach (var part in _body.GetBodyPartChildren(removedPart))
        {
            if (!TryComp<WoundableVisualsComponent>(part.Id, out var woundableVisuals))
                continue;

            foreach (var (group, _) in woundableVisuals.DamageOverlayGroups!)
            {
                if (!_sprite.LayerMapTryGet(bodyEnt, $"{woundableVisuals.OccupiedLayer}{group}", out var layer, false))
                    continue;

                _sprite.LayerSetVisible(bodyEnt, layer, false);
                _sprite.RemoveLayer(bodyEnt, layer);
            }

            if (_sprite.LayerMapTryGet(bodyEnt, $"{woundableVisuals.OccupiedLayer}Bleeding", out var childBleeds, false))
            {
                _sprite.LayerSetVisible(bodyEnt, childBleeds, false);
                _sprite.RemoveLayer(bodyEnt, childBleeds);
            }

            if (TryComp(part.Id, out SpriteComponent? pieceSprite))
                UpdateWoundableVisuals(part.Id, woundableVisuals, pieceSprite);
        }
    }

    private void OnWoundableIntegrityChanged(EntityUid uid, WoundableVisualsComponent component, ref WoundableIntegrityChangedEvent args)
    {
        if (!TryGetWoundableBody(uid, out var bodyUid))
        {
            if (TryComp(uid, out SpriteComponent? partSprite))
                UpdateWoundableVisuals(uid, component, partSprite);
            return;
        }

        if (TryComp(bodyUid, out SpriteComponent? bodySprite))
            UpdateWoundableVisuals(uid, component, bodySprite);
    }

    private bool TryGetWoundableBody(EntityUid woundableUid, out EntityUid bodyUid)
    {
        if (TryComp<BodyPartComponent>(woundableUid, out var bodyPart) && bodyPart.Body is { } partBody)
        {
            bodyUid = partBody;
            return true;
        }

        if (TryComp<OrganComponent>(woundableUid, out var organ) && organ.Body is { } organBody)
        {
            bodyUid = organBody;
            return true;
        }

        bodyUid = default;
        return false;
    }

    private void EnsureDamageLayersOnSprite(WoundableVisualsComponent component, SpriteComponent bodySprite)
    {
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

        if (!bodySprite.LayerMapTryGet($"{component.OccupiedLayer}Bleeding", out _) && component.BleedingOverlay != null)
        {
            AddDamageLayerToSprite(bodySprite,
                component.BleedingOverlay,
                $"{component.OccupiedLayer}_Minor",
                $"{component.OccupiedLayer}Bleeding");
        }
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

    private void UpdateWoundableVisuals(EntityUid uid, WoundableVisualsComponent visuals, SpriteComponent sprite)
    {
        foreach (var group in visuals.DamageOverlayGroups!)
        {
            if (!sprite.LayerMapTryGet($"{visuals.OccupiedLayer}{group.Key}", out var damageLayer))
                continue;

            var severityPoint = _wound.GetWoundableSeverityPoint(uid, damageGroup: group.Key);
            UpdateDamageLayerState(sprite,
                damageLayer,
                $"{visuals.OccupiedLayer}_{group.Key}",
                severityPoint < visuals.Thresholds.First() ? 0 : GetThreshold(severityPoint, visuals));
        }

        UpdateBleeding(uid, visuals, visuals.OccupiedLayer, sprite);
    }

    private void UpdateBleeding(EntityUid uid, WoundableVisualsComponent comp, Enum layer, SpriteComponent sprite)
    {
        if (!TryComp<BodyPartComponent>(uid, out var bodyPart))
            return;

        if (comp.BleedingOverlay == null)
        {
            if (!_body.TryGetParentBodyPart(uid, out var parentUid, out _))
                return;

            var totalBleeds = FixedPoint2.Zero;
            foreach (var woundEnt in _wound.GetWoundableWounds(parentUid.Value))
            {
                if (!TryComp<BleedInflicterComponent>(woundEnt, out var bleeds) || !bleeds.IsBleeding)
                    continue;

                totalBleeds += bleeds.BleedingAmount;
            }

            foreach (var woundEnt in _wound.GetWoundableWounds(uid))
            {
                if (!TryComp<BleedInflicterComponent>(woundEnt, out var bleeds) || !bleeds.IsBleeding)
                    continue;

                totalBleeds += bleeds.BleedingAmount;
            }

            var symmetry = bodyPart.Symmetry == BodyPartSymmetry.Left ? "L" : "R";
            var partType = bodyPart.PartType == BodyPartType.Foot ? "Leg" : "Arm";

            var part = symmetry + partType;

            if (sprite.LayerMapTryGet($"{part}Bleeding", out var parentBleedingLayer))
            {
                UpdateBleedingLayerState(
                    sprite,
                    parentBleedingLayer,
                    part,
                    totalBleeds,
                    GetBleedingThreshold(totalBleeds, comp));
            }
        }
        else
        {
            var totalBleeds = FixedPoint2.Zero;
            foreach (var woundEnt in _wound.GetWoundableWounds(uid))
            {
                if (!TryComp<BleedInflicterComponent>(woundEnt, out var bleeds) || !bleeds.IsBleeding)
                    continue;

                totalBleeds += bleeds.BleedingAmount;
            }

            if (sprite.LayerMapTryGet($"{layer}Bleeding", out var bleedingLayer))
            {
                UpdateBleedingLayerState(sprite,
                    bleedingLayer,
                    layer.ToString(),
                    totalBleeds,
                    GetBleedingThreshold(totalBleeds, comp));
            }
        }
    }

    private FixedPoint2 GetThreshold(FixedPoint2 threshold, WoundableVisualsComponent comp)
    {
        var nearestSeverity = FixedPoint2.Zero;

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
