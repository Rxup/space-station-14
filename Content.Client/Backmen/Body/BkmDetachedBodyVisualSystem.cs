using Content.Client.Backmen.Surgery.Wounds;
using Content.Shared.Backmen.Body.OrganRelations;
using Content.Shared.Backmen.Targeting;
using Content.Shared.Body;
using Content.Shared.Body.Part;
using Content.Shared.Humanoid;
using Robust.Client.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Client.Backmen.Body;

/// <summary>
/// Ensures detached limb bundles show organ layers after organs are moved into the runtime shell.
/// </summary>
public sealed partial class BkmDetachedBodyVisualSystem : EntitySystem
{
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private SharedContainerSystem _containers = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BkmDetachedBodyComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<BkmDetachedBodyComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<BkmDetachedBodyComponent, EntInsertedIntoContainerMessage>(OnContainerInserted);
        SubscribeLocalEvent<BkmDetachedBodyComponent, AfterAutoHandleStateEvent>(OnState);
    }

    private void OnStartup(Entity<BkmDetachedBodyComponent> ent, ref ComponentStartup args) =>
        RefreshVisuals(ent);

    private void OnMapInit(Entity<BkmDetachedBodyComponent> ent, ref MapInitEvent args) =>
        RefreshVisuals(ent);

    private void OnContainerInserted(Entity<BkmDetachedBodyComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID != BodyComponent.ContainerID)
            return;

        RefreshVisuals(ent);
    }

    private void OnState(Entity<BkmDetachedBodyComponent> ent, ref AfterAutoHandleStateEvent args) =>
        RefreshVisuals(ent);

    private void RefreshVisuals(Entity<BkmDetachedBodyComponent> ent)
    {
        if (!_containers.TryGetContainer(ent, BodyComponent.ContainerID, out var organContainer))
            return;

        foreach (var organ in organContainer.ContainedEntities)
            ApplyOrganVisuals(ent, organ);
    }

    private void ApplyOrganVisuals(EntityUid bundle, EntityUid organ)
    {
        var inserted = new OrganGotInsertedEvent(bundle);

        if (HasComp<VisualOrganComponent>(organ))
            RaiseLocalEvent(organ, ref inserted);

        if (HasComp<VisualOrganMarkingsComponent>(organ))
            RaiseLocalEvent(organ, ref inserted);

        // World-sprite fallback is only for external limbs that intentionally omit VisualOrgan
        // (e.g. spider legs). Internal organs (ears, tongue, brain, …) must not paint their
        // item sprites onto the bundle — that drew ears as a spiral under detached heads.
        if (!HasComp<VisualOrganComponent>(organ) && ShouldApplyWorldSpriteOrganVisual(organ))
            ApplySpriteOrganVisual(bundle, organ);
    }

    private bool ShouldApplyWorldSpriteOrganVisual(EntityUid organ)
    {
        if (!TryComp(organ, out OrganComponent? organComp) || organComp.Category is not { } category)
            return false;

        return SurgeryBodyPartMapping.IsExternalCategory(category);
    }

    /// <summary>
    /// Organs with only a world sprite (e.g. spider legs) must not use <see cref="VisualOrganComponent"/>
    /// on living bodies — that would fight over shared humanoid layers. Detached bundles are the exception.
    /// </summary>
    private void ApplySpriteOrganVisual(EntityUid body, EntityUid organ)
    {
        if (!TryComp(body, out SpriteComponent? bodySprite)
            || !TryComp(organ, out SpriteComponent? organSprite))
        {
            return;
        }

        var organRsi = organSprite.LayerGetActualRSI(0) ?? organSprite.BaseRSI;
        if (organRsi == null)
            return;

        if (!TryResolveDetachedOrganLayer(organ, out var layer))
            return;

        if (!_sprite.LayerMapTryGet((body, bodySprite), layer, out var index, true))
            return;

        _sprite.LayerSetData((body, bodySprite), index, new PrototypeLayerData
        {
            RsiPath = organRsi.Path.ToString(),
            State = _sprite.LayerGetRsiState((organ, organSprite), 0).Name,
        });
        _sprite.LayerSetVisible((body, bodySprite), index, true);
    }

    private bool TryResolveDetachedOrganLayer(EntityUid organ, out Enum layer)
    {
        if (TryComp(organ, out WoundableVisualsComponent? visuals))
        {
            layer = visuals.OccupiedLayer;
            return true;
        }

        layer = HumanoidVisualLayers.Chest;

        if (!TryComp(organ, out OrganComponent? organComp) || organComp.Category is not { } category)
            return false;

        if (SurgeryBodyPartMapping.IsSpiderLegCategory(category))
        {
            layer = category.Id.StartsWith("SpiderLegRight", StringComparison.Ordinal)
                ? HumanoidVisualLayers.RLeg
                : HumanoidVisualLayers.LLeg;
            return true;
        }

        if (!SurgeryBodyPartMapping.TryGetBodyPartType(category, out var type, out var symmetry))
            return false;

        layer = type switch
        {
            BodyPartType.Head => HumanoidVisualLayers.Head,
            BodyPartType.Chest or BodyPartType.Groin => HumanoidVisualLayers.Chest,
            BodyPartType.Arm => symmetry == BodyPartSymmetry.Right
                ? HumanoidVisualLayers.RArm
                : HumanoidVisualLayers.LArm,
            BodyPartType.Hand => symmetry == BodyPartSymmetry.Right
                ? HumanoidVisualLayers.RHand
                : HumanoidVisualLayers.LHand,
            BodyPartType.Leg => symmetry == BodyPartSymmetry.Right
                ? HumanoidVisualLayers.RLeg
                : HumanoidVisualLayers.LLeg,
            BodyPartType.Foot => symmetry == BodyPartSymmetry.Right
                ? HumanoidVisualLayers.RFoot
                : HumanoidVisualLayers.LFoot,
            _ => HumanoidVisualLayers.Chest,
        };

        return type is BodyPartType.Head
            or BodyPartType.Chest
            or BodyPartType.Groin
            or BodyPartType.Arm
            or BodyPartType.Hand
            or BodyPartType.Leg
            or BodyPartType.Foot;
    }
}
