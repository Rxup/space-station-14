using Content.Server.Backmen.Destructible.Thresholds.Behaviors;
using Content.Shared.Backmen.Surgery;
using Content.Shared.Body;
using Content.Shared.IdentityManagement;
using Content.Shared.Inventory;
using Content.Shared.Popups;
using JetBrains.Annotations;
using Robust.Server.GameObjects;

namespace Content.Server.Destructible.Thresholds.Behaviors;

[UsedImplicitly]
[DataDefinition]
public sealed partial class BurnBodyBehavior : IThresholdBehavior
{

    public void Execute(EntityUid bodyId, DestructibleSystem system, EntityUid? cause = null)
    {
        // start-backmen: detached-body-burn
        if (system.EntityManager.HasComponent<SurgeryTargetComponent>(bodyId)
            && system.EntityManager.HasComponent<BodyComponent>(bodyId))
        {
            new BkmBurnBodyBehavior().Execute(bodyId, system, cause);
            return;
        }
        // end-backmen: detached-body-burn

        var transformSystem = system.EntityManager.System<TransformSystem>();
        var inventorySystem = system.EntityManager.System<InventorySystem>();
        var sharedPopupSystem = system.EntityManager.System<SharedPopupSystem>();

        if (system.EntityManager.TryGetComponent<InventoryComponent>(bodyId, out var comp))
        {
            foreach (var item in inventorySystem.GetHandOrInventoryEntities(bodyId))
            {
                transformSystem.DropNextTo(item, bodyId);
            }
        }

        var bodyIdentity = Identity.Entity(bodyId, system.EntityManager);
        sharedPopupSystem.PopupCoordinates(Loc.GetString("bodyburn-text-others", ("name", bodyIdentity)), transformSystem.GetMoverCoordinates(bodyId), PopupType.LargeCaution);

        system.EntityManager.QueueDeleteEntity(bodyId);
    }
}
