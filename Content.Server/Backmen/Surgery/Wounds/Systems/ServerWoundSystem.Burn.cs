using System.Linq;
using Content.Server.Backmen.Body;
using Content.Server.Backmen.Body.Systems;
using Content.Shared.Backmen.Body.OrganRelations;
using Content.Shared.Backmen.Surgery.Traumas;
using Content.Shared.Backmen.Surgery.Traumas.Components;
using Content.Shared.Backmen.Surgery.Wounds;
using Content.Shared.Backmen.Surgery.Wounds.Components;
using Content.Shared.Backmen.Targeting;
using Content.Shared.Body;
using Content.Shared.Body.Part;
using Content.Shared.Inventory;

namespace Content.Server.Backmen.Surgery.Wounds.Systems;

public sealed partial class ServerWoundSystem
{
    [Dependency] private BkmBurnWoundableSystem _burnWoundable = default!;
    [Dependency] private BkmBurnEffectsSystem _burnEffects = default!;
    [Dependency] private BkmBrainPreservationSystem _brainPreserve = default!;

    private void BurnWoundableToAsh(
        EntityUid parentWoundableEntity,
        EntityUid woundableEntity,
        WoundableComponent woundableComp,
        WoundableComponent? parentWoundableComp)
    {
        if (!Body.TryGetWoundableBodyPartInfo(woundableEntity, out var bodyUid, out var partType, out _))
        {
            SpawnPartBurnEffects(woundableEntity);
            QueueDel(woundableEntity);
            return;
        }

        woundableComp.AllowWounds = false;
        woundableComp.WoundableSeverity = WoundableSeverity.Loss;
        DirtyFields(woundableEntity, woundableComp, null,
            nameof(WoundableComponent.AllowWounds),
            nameof(WoundableComponent.WoundableSeverity));

        if (TryComp<TargetingComponent>(bodyUid, out var targeting))
            RefreshBodyTargetingStatus(bodyUid, targeting);

        Audio.PlayPvs(woundableComp.WoundableDestroyedSound, bodyUid);

        PreserveBrainsOnWoundable(woundableEntity);

        if (IsWoundableRoot(woundableEntity, woundableComp))
        {
            DropWoundableOrgans(woundableEntity, woundableComp);
            DestroyWoundableChildren(woundableEntity, woundableComp);
            SpawnPartBurnEffects(woundableEntity);

            if (TryComp<OrganComponent>(woundableEntity, out var rootOrgan))
            {
                using var _ = EntityManager.System<DetachableOrganSystem>().EnterBurnDestroy();
                Body.RemoveOrgan(woundableEntity, rootOrgan);
            }

            QueueDel(woundableEntity);

            if (EntityManager.System<BkmSurgeryDestructibleSystem>().ShouldFullBodyGib(bodyUid))
                Body.GibBody(bodyUid);
        }
        else
        {
            if (TryComp<InventoryComponent>(bodyUid, out var inventory)
                && Body.GetBodyPartCount(bodyUid, partType) == 1
                && Body.TryGetPartSlotContainerName(partType, out var containerNames))
            {
                foreach (var containerName in containerNames)
                    Inventory.DropSlotContents(bodyUid, containerName, inventory);
            }

            if (partType is BodyPartType.Hand or BodyPartType.Arm)
                Hands.TryDrop(bodyUid, woundableEntity);

            DropWoundableOrgans(woundableEntity, woundableComp);
            DestroyWoundableChildren(woundableEntity, woundableComp);
            SpawnPartBurnEffects(woundableEntity);

            foreach (var wound in GetWoundableWounds(woundableEntity, woundableComp))
                TransferWoundDamage(parentWoundableEntity, woundableEntity, wound, parentWoundableComp, wound);

            if (parentWoundableComp != null
                && TryInduceWound(parentWoundableEntity, BluntWoundId, 15f, out var woundEnt, parentWoundableComp))
            {
                Trauma.AddTrauma(
                    parentWoundableEntity,
                    (parentWoundableEntity, parentWoundableComp),
                    (woundEnt.Value.Owner, EnsureComp<TraumaInflicterComponent>(woundEnt.Value.Owner)),
                    TraumaType.Dismemberment,
                    15f);
            }

            if (TerminatingOrDeleted(parentWoundableEntity))
                return;

            foreach (var wound in
                     GetWoundableWoundsWithComp<BleedInflicterComponent>(parentWoundableEntity, parentWoundableComp))
                wound.Comp2.ScalingLimit += 4;

            if (TryComp<OrganComponent>(woundableEntity, out var organ))
            {
                using var _ = EntityManager.System<DetachableOrganSystem>().EnterBurnDestroy();
                Body.RemoveOrgan(woundableEntity, organ);
            }

            QueueDel(woundableEntity);
        }
    }

    private void PreserveBrainsOnWoundable(EntityUid woundable)
    {
        foreach (var (organUid, _) in Body.GetOrgansForWoundable(woundable).ToList())
        {
            if (HasComp<BrainComponent>(organUid))
                _brainPreserve.TryPreserveBrain(organUid, Transform(woundable).Coordinates);
        }
    }

    private void SpawnPartBurnEffects(EntityUid woundable)
    {
        var coords = Transform(woundable).Coordinates;
        _burnEffects.SpawnAshAt(coords);
        _burnEffects.PlayBurnSound(woundable);
        _burnEffects.PopupPartBurn(woundable, coords);
    }
}
