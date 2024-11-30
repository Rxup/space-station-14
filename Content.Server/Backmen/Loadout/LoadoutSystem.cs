using Content.Corvax.Interfaces.Server;
using Content.Corvax.Interfaces.Shared;
using Content.Server.GameTicking;
using Content.Server.Hands.Systems;
using Content.Server.Storage.EntitySystems;
using Content.Shared.Clothing.Components;
using Content.Shared.GameTicking;
using Content.Shared.Inventory;
using Robust.Shared.Prototypes;

namespace Content.Server.Backmen.Loadout;

public sealed class LoadoutSystem : EntitySystem
{
    private const string BackpackSlotId = "back";

    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly InventorySystem _inventorySystem = default!;
    [Dependency] private readonly HandsSystem _handsSystem = default!;
    [Dependency] private readonly StorageSystem _storageSystem = default!;
    [Dependency] private readonly ISharedSponsorsManager _sponsorsManager = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawned);
    }

    private void OnPlayerSpawned(PlayerSpawnCompleteEvent ev)
    {
        if (!_sponsorsManager.TryGetServerPrototypes(ev.Player.UserId, out var prototypes))
            return;

        foreach (var loadoutId in prototypes)
        {
            // NOTE: Now is easy to not extract method because event give all info we need
            if (!_prototypeManager.TryIndex<LoadoutItemPrototype>(loadoutId, out var loadout))
                continue;

            var isSponsorOnly = loadout.SponsorOnly &&
                                !prototypes.Contains(loadoutId);
            var isWhitelisted = ev.JobId != null &&
                                loadout.WhitelistJobs != null &&
                                !loadout.WhitelistJobs.Contains(ev.JobId);
            var isBlacklisted = ev.JobId != null &&
                                loadout.BlacklistJobs != null &&
                                loadout.BlacklistJobs.Contains(ev.JobId);
            var isSpeciesRestricted = loadout.SpeciesRestrictions != null &&
                                      loadout.SpeciesRestrictions.Contains(ev.Profile.Species);

            if (isSponsorOnly || isWhitelisted || isBlacklisted || isSpeciesRestricted)
                continue;

            var entity = Spawn(loadout.EntityId, Transform(ev.Mob).Coordinates);

            // Take in hand if not clothes
            if (!TryComp<ClothingComponent>(entity, out var clothing) || !TryComp<InventoryComponent>(ev.Mob, out var inventoryComponent))
            {
                if(!_handsSystem.TryPickup(ev.Mob, entity))
                    QueueDel(entity);
                continue;
            }

            // Automatically search empty slot for clothes to equip
            var firstSlotName = (string?)null;
            var isEquiped = false;

            if (!_inventorySystem.TryGetSlots(ev.Mob, out var slotDefinitions))
                return;

            foreach (var slot in slotDefinitions)
            {
                if (!clothing.Slots.HasFlag(slot.SlotFlags))
                    continue;

                firstSlotName ??= slot.Name;

                if (_inventorySystem.TryGetSlotEntity(ev.Mob, slot.Name, out var _, inventoryComponent))
                    continue;

                if (!_inventorySystem.TryEquip(ev.Mob, entity, slot.Name, true, force: true, clothing: clothing, inventory: inventoryComponent))
                    continue;

                isEquiped = true;
                break;
            }

            if (isEquiped || firstSlotName == null)
                continue;

            // Force equip to first valid clothes slot
            // Get occupied entity -> Insert to backpack -> Equip loadout entity
            if (_inventorySystem.TryGetSlotEntity(ev.Mob, firstSlotName, out var slotEntity) &&
                _inventorySystem.TryGetSlotEntity(ev.Mob, BackpackSlotId, out var backEntity) &&
                _storageSystem.CanInsert(backEntity.Value, slotEntity.Value, out _))
            {
                if(!_storageSystem.Insert(backEntity.Value, slotEntity.Value, out _, playSound: false))
                    continue;
            }

            if (!_inventorySystem.TryEquip(ev.Mob, entity, firstSlotName, true, force: true, clothing: clothing, inventory: inventoryComponent))
            {
                QueueDel(entity);
            }
        }
    }
}
