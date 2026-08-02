using Content.Shared.Clothing.Components;
using Content.Shared.Clothing.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Nutrition;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems; // backmen: clear-ingestion-blockers

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
    private IngestionSystem _ingestion = default!; // backmen: clear-ingestion-blockers

    private static readonly SlotFlags MouthSlots = SlotFlags.HEAD | SlotFlags.MASK;
    private const int MaxClearAttempts = 4; // backmen: clear-ingestion-blockers

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _inventory = sysManager.GetEntitySystem<InventorySystem>();
        _mask = sysManager.GetEntitySystem<MaskSystem>();
        _toggleableClothing = sysManager.GetEntitySystem<ToggleableClothingSystem>();
        _ingestion = sysManager.GetEntitySystem<IngestionSystem>(); // backmen: clear-ingestion-blockers
    }

    public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
    {
        var subject = TargetKey != null && blackboard.TryGetValue<EntityUid>(TargetKey, out var target, _entManager)
            ? target
            : blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        // start-backmen: clear-ingestion-blockers
        if (!IsMouthBlocked(subject, out _))
            return HTNOperatorStatus.Finished;

        TryClearFaceProtection(subject);

        return IsMouthBlocked(subject, out _) ? HTNOperatorStatus.Failed : HTNOperatorStatus.Finished;
        // end-backmen: clear-ingestion-blockers
    }

    // start-backmen: clear-ingestion-blockers
    private bool IsMouthBlocked(EntityUid uid, out EntityUid? blocker)
    {
        blocker = null;

        if (!_entManager.TryGetComponent<InventoryComponent>(uid, out var inventory))
            return false;

        var attempt = new IngestionAttemptEvent(MouthSlots);
        _inventory.RelayEvent((uid, inventory), ref attempt);
        blocker = attempt.Blocker;
        return attempt.Cancelled;
    }

    private void TryClearFaceProtection(EntityUid owner)
    {
        // Soft clear first: pull masks down so they keep functioning as breath tools when possible.
        TryPullDownMask(owner);

        for (var i = 0; i < MaxClearAttempts && IsMouthBlocked(owner, out var blocker); i++)
        {
            if (blocker is not { } blockerUid)
            {
                // Unknown blocker — strip both mouth slots as a fallback.
                ForceClearSlot(owner, "head");
                ForceClearSlot(owner, "mask");
                break;
            }

            if (!_inventory.TryGetContainingSlot(blockerUid, out var slotDef))
            {
                // Blocker isn't in a known slot anymore; disable it in place.
                _ingestion.SetBlockerEnabled(blockerUid, false);
                continue;
            }

            var slot = slotDef.Name;

            // Hardsuit helmets / hoods: stow back into the parent clothing when possible.
            if (_toggleableClothing.TryStowAttached(owner, slot) && !IsMouthBlocked(owner, out _))
                continue;

            // Force unequip (covers muzzles, non-toggle masks, loose helmets, failed stows).
            if (_inventory.TryUnequip(owner, slot, silent: true, force: true) && !IsMouthBlocked(owner, out _))
                continue;

            // Last resort: leave the item on but stop it from blocking ingestion.
            _ingestion.SetBlockerEnabled(blockerUid, false);
        }
    }
    // end-backmen: clear-ingestion-blockers

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

    // start-backmen: clear-ingestion-blockers
    private void ForceClearSlot(EntityUid owner, string slot)
    {
        if (_toggleableClothing.TryStowAttached(owner, slot))
            return;

        if (!_inventory.TryGetSlotEntity(owner, slot, out _))
            return;

        _inventory.TryUnequip(owner, slot, silent: true, force: true);
    }
    // end-backmen: clear-ingestion-blockers
}
