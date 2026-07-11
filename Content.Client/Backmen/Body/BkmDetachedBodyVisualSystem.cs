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

        if (!HasComp<VisualOrganComponent>(organ))
            ApplySpriteOrganVisual(bundle, organ);
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

        var layer = ResolveDetachedOrganLayer(organ);
        if (!_sprite.LayerMapTryGet((body, bodySprite), layer, out var index, true))
            return;

        _sprite.LayerSetData((body, bodySprite), index, new PrototypeLayerData
        {
            RsiPath = organRsi.Path.ToString(),
            State = _sprite.LayerGetRsiState((organ, organSprite), 0).Name,
        });
        _sprite.LayerSetVisible((body, bodySprite), index, true);
    }

    private Enum ResolveDetachedOrganLayer(EntityUid organ)
    {
        if (TryComp(organ, out WoundableVisualsComponent? visuals))
            return visuals.OccupiedLayer;

        if (!TryComp(organ, out OrganComponent? organComp) || organComp.Category is not { } category)
            return HumanoidVisualLayers.Chest;

        if (SurgeryBodyPartMapping.IsSpiderLegCategory(category))
        {
            return category.Id.StartsWith("SpiderLegRight", StringComparison.Ordinal)
                ? HumanoidVisualLayers.RLeg
                : HumanoidVisualLayers.LLeg;
        }

        if (SurgeryBodyPartMapping.TryGetBodyPartType(category, out var type, out var symmetry))
        {
            return type switch
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
        }

        return HumanoidVisualLayers.Chest;
    }
}
