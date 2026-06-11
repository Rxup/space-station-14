using Content.Shared.Clothing.Components;
using Content.Shared.Clothing.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Nutrition;
using Content.Shared.Nutrition.Components;

namespace Content.Server.NPC.HTN.PrimitiveTasks.Operators.Interactions;

/// <summary>
/// Pulls down toggleable masks or unequips head/mask items that block eating and drinking.
/// </summary>
public sealed partial class ClearIngestionBlockersOperator : HTNOperator
{
    [Dependency] private IEntityManager _entManager = default!;
    private InventorySystem _inventory = default!;
    private MaskSystem _mask = default!;

    private static readonly SlotFlags MouthSlots = SlotFlags.HEAD | SlotFlags.MASK;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _inventory = sysManager.GetEntitySystem<InventorySystem>();
        _mask = sysManager.GetEntitySystem<MaskSystem>();
    }

    public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        if (!IsMouthBlocked(owner))
            return HTNOperatorStatus.Finished;

        TryClearMask(owner);

        if (!IsMouthBlocked(owner))
            return HTNOperatorStatus.Finished;

        TryUnequipBlocker(owner, "head");

        return IsMouthBlocked(owner) ? HTNOperatorStatus.Failed : HTNOperatorStatus.Finished;
    }

    private bool IsMouthBlocked(EntityUid uid)
    {
        var attempt = new IngestionAttemptEvent(MouthSlots);
        _entManager.EventBus.RaiseLocalEvent(uid, ref attempt);
        return attempt.Cancelled;
    }

    private void TryClearMask(EntityUid owner)
    {
        if (!_inventory.TryGetSlotEntity(owner, "mask", out var maskUid) || maskUid is not { } mask)
            return;

        if (_entManager.TryGetComponent<MaskComponent>(mask, out var maskComp)
            && maskComp.IsToggleable
            && !maskComp.IsToggled)
        {
            _mask.SetToggled(mask, true);
            return;
        }

        TryUnequipBlocker(owner, "mask");
    }

    private void TryUnequipBlocker(EntityUid owner, string slot)
    {
        if (!_inventory.TryGetSlotEntity(owner, slot, out var itemUid) || itemUid is not { } item)
            return;

        if (!_entManager.TryGetComponent<IngestionBlockerComponent>(item, out var blocker) || !blocker.Enabled)
            return;

        _inventory.TryUnequip(owner, slot, silent: true, force: true);
    }
}
