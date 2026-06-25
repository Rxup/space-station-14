using Content.Server.Backmen.Destructible.Thresholds.Behaviors;
using Content.Shared.Backmen.Body.Systems;
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
    /// <summary>
    ///     The popup displayed upon destruction.
    /// </summary>
    [DataField]
    public LocId PopupMessage = "bodyburn-text-others";

    public void Execute(EntityUid bodyId, DestructibleSystem system, EntityUid? cause = null)
    {
        // start-backmen: detached-body-burn
        var bodySys = system.EntityManager.System<BkmBodySharedSystem>();
        if (system.EntityManager.HasComponent<BodyComponent>(bodyId)
            && !bodySys.UsesFlatOrgans(bodyId))
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
        sharedPopupSystem.PopupCoordinates(Loc.GetString(PopupMessage, ("name", bodyIdentity)), transformSystem.GetMoverCoordinates(bodyId), PopupType.LargeCaution);

        system.EntityManager.QueueDeleteEntity(bodyId);
    }
}
