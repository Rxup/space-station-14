using Content.Server.Atmos.EntitySystems;
using Content.Server.Backmen.Body.Systems;
using Content.Server.Destructible;
using Content.Server.Destructible.Thresholds.Behaviors;
using Content.Shared.Backmen.Body.OrganRelations;
using Content.Shared.Backmen.Surgery;
using Content.Shared.Body;
using Content.Shared.IdentityManagement;
using Content.Shared.Inventory;
using Content.Shared.Popups;
using JetBrains.Annotations;
using Robust.Server.GameObjects;

namespace Content.Server.Backmen.Destructible.Thresholds.Behaviors;

[UsedImplicitly]
[DataDefinition]
public sealed partial class BkmBurnBodyBehavior : IThresholdBehavior
{
    public void Execute(EntityUid bodyId, DestructibleSystem system, EntityUid? cause = null)
    {
        var entityManager = system.EntityManager;
        var transformSystem = entityManager.System<TransformSystem>();
        var inventorySystem = entityManager.System<InventorySystem>();
        var sharedPopupSystem = entityManager.System<SharedPopupSystem>();
        var bodySystem = entityManager.System<BkmBodySystem>();
        var flammableSystem = entityManager.System<FlammableSystem>();

        if (entityManager.TryGetComponent<InventoryComponent>(bodyId, out var comp))
        {
            foreach (var item in inventorySystem.GetHandOrInventoryEntities(bodyId))
                transformSystem.DropNextTo(item, bodyId);
        }

        var bodyIdentity = Identity.Entity(bodyId, entityManager);
        sharedPopupSystem.PopupCoordinates(
            Loc.GetString("bodyburn-text-others", ("name", bodyIdentity)),
            transformSystem.GetMoverCoordinates(bodyId),
            PopupType.LargeCaution);

        if (entityManager.HasComponent<SurgeryTargetComponent>(bodyId)
            && entityManager.HasComponent<BodyComponent>(bodyId))
        {
            var gibs = bodySystem.GibBody(bodyId, gibOrgans: true, launchGibs: true, splatModifier: 2f);

            foreach (var gib in gibs)
            {
                if (!entityManager.HasComponent<BkmDetachedBodyComponent>(gib))
                    continue;

                flammableSystem.Ignite(gib, cause ?? bodyId);
            }

            entityManager.QueueDeleteEntity(bodyId);
            return;
        }

        entityManager.QueueDeleteEntity(bodyId);
    }
}
