using System.Numerics;
using Content.Server.Popups;
using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.Interaction.Components;
using Content.Shared.Inventory;
using Content.Shared.Popups;
using Content.Shared.Slippery;
using Content.Shared.Throwing;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Random;

namespace Content.Server._Cats.Slippery;

public sealed class DropOnSlipSystem : EntitySystem
{
    [Dependency] private readonly ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;

    public float PocketDropChance = 10f;
    public float PocketThrowChance = 5f;

    public float ClumsyDropChance = 5f;
    public float ClumsyThrowChance = 90f;


    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<InventoryComponent, ParkSlipEvent>(HandleSlip);
    }


    private void HandleSlip(EntityUid entity, InventoryComponent invComp, ParkSlipEvent args)
    {
        if (!_inventory.TryGetSlots(entity, out var slotDefinitions))
            return;

        foreach (var slot in slotDefinitions)
        {
            if (!_inventory.TryGetSlotEntity(entity, slot.Name, out var item))
                continue;

            if (ShouldBeDropped(entity, slot, item))
            {
                var popupString = Loc.GetString("system-drop-on-slip-text-component", ("name", entity), ("item", item));

                Drop(entity, item.Value, slot.Name, popupString);
            }
        }
    }

    private bool ShouldBeDropped(EntityUid entity, SlotDefinition slot, EntityUid? item)
    {
        // Check for any items in pockets or other criteria
        if (slot.SlotFlags == SlotFlags.POCKET && _random.NextFloat(0, 100) < PocketDropChance)
            return true;

        // Check for DropOnSlipComponent
        if (EntityManager.TryGetComponent<_Cats.Slippery.DropOnSlipComponent>(item, out var dropComp) && _random.NextFloat(0, 100) < dropComp.Chance)
            return true;

        // Check for ClumsyComponent
        if (slot.Name != "jumpsuit" && _random.NextFloat(0, 100) < ClumsyDropChance && HasComp<ClumsyComponent>(entity))
            return true;

        return false;
    }

    private void Drop(EntityUid entity, EntityUid item, string slot, string popupString)
    {
        if (!_inventory.TryUnequip(entity, slot, false, true))
            return;

        EntityManager.TryGetComponent<PhysicsComponent>(entity, out var entPhysComp);

        if (entPhysComp != null)
        {
            var strength = entPhysComp.LinearVelocity.Length() / 1.5f;
            Vector2 direction = new Vector2(_random.Next(-8, 8), _random.Next(-8, 8));

            _throwing.TryThrow(item, direction, strength, entity);
        }

        _popup.PopupEntity(popupString, item, PopupType.MediumCaution);

        var logMessage = Loc.GetString("system-drop-on-slip-log", ("entity", ToPrettyString(entity)), ("item", ToPrettyString(item)));
        _adminLogger.Add(LogType.Slip, LogImpact.Low, $"{logMessage}");
    }
}