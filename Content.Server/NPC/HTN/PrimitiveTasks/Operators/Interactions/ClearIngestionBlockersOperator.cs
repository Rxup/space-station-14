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

    /// <summary>
    /// Blackboard key for the entity to clear blockers on. Defaults to the NPC owner (e.g. before eating).
    /// </summary>
    [DataField("targetKey")]
    public string? TargetKey;

    private InventorySystem _inventory = default!;
    private MaskSystem _mask = default!;
    private ToggleableClothingSystem _toggleableClothing = default!;

    private static readonly SlotFlags MouthSlots = SlotFlags.HEAD | SlotFlags.MASK;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _inventory = sysManager.GetEntitySystem<InventorySystem>();
        _mask = sysManager.GetEntitySystem<MaskSystem>();
        _toggleableClothing = sysManager.GetEntitySystem<ToggleableClothingSystem>();
    }

    public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
    {
        var subject = TargetKey != null && blackboard.TryGetValue<EntityUid>(TargetKey, out var target, _entManager)
            ? target
            : blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        if (!IsMouthBlocked(subject))
            return HTNOperatorStatus.Finished;

        TryClearFaceProtection(subject);

        return IsMouthBlocked(subject) ? HTNOperatorStatus.Failed : HTNOperatorStatus.Finished;
    }

    private bool IsMouthBlocked(EntityUid uid)
    {
        if (!_entManager.TryGetComponent<InventoryComponent>(uid, out var inventory))
            return false;

        var attempt = new IngestionAttemptEvent(MouthSlots);
        _inventory.RelayEvent((uid, inventory), ref attempt);
        return attempt.Cancelled;
    }

    private void TryClearFaceProtection(EntityUid owner)
    {
        TryPullDownMask(owner);

        if (IsMouthBlocked(owner))
            TryUnequipBlocker(owner, "head");

        if (IsMouthBlocked(owner))
            TryUnequipBlocker(owner, "mask");
    }

    private void TryPullDownMask(EntityUid owner)
    {
        if (!_inventory.TryGetSlotEntity(owner, "mask", out var maskUid) || maskUid is not { } mask)
            return;

        if (!_entManager.TryGetComponent<MaskComponent>(mask, out var maskComp) || maskComp.IsToggled)
            return;

        _mask.SetToggled(mask, true);

        // Non-toggleable masks still have MaskComponent but ignore SetToggled without force.
        if (_entManager.TryGetComponent<MaskComponent>(mask, out maskComp) && !maskComp.IsToggled)
            _mask.SetToggled(mask, true, force: true);
    }

    private void TryUnequipBlocker(EntityUid owner, string slot)
    {
        if (_toggleableClothing.TryStowAttached(owner, slot))
            return;

        if (!_inventory.TryGetSlotEntity(owner, slot, out var itemUid) || itemUid is not { } item)
            return;

        if (!_entManager.TryGetComponent<IngestionBlockerComponent>(item, out var blocker) || !blocker.Enabled)
            return;

        _inventory.TryUnequip(owner, slot, silent: true, force: true);
    }
}
