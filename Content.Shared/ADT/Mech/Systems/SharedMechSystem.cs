using Content.Shared.Mech.Components;
using Content.Shared.ADT.Mech;
using Content.Shared.Storage;

namespace Content.Shared.Mech.EntitySystems;

/// <summary>
/// Handles all of the interactions, UI handling, and items shennanigans for <see cref="MechComponent"/>
/// </summary>
public abstract partial class SharedMechSystem
{
    private void InitializeADT()
    {
        SubscribeLocalEvent<MechComponent, MechGunReloadMessage>(ReceiveEquipmentUiMesssages);
        SubscribeLocalEvent<MechComponent, StorageInteractAttemptEvent>(OnStorageInteract);
    }

    /// <summary>
    /// Handles mech equipment selection in radial menu.
    /// </summary>
    /// <param name="ev"></param>
    private void OnMechEquipSelected(SelectMechEquipmentEvent ev)
    {
        var uid = GetEntity(ev.User);

        if (!TryComp<MechPilotComponent>(uid, out var pilot))
            return;

        var mechUid = pilot.Mech;
        if (!TryComp<MechComponent>(mechUid, out var mech))
            return;

        if (ev.Equipment == null)
        {
            mech.CurrentSelectedEquipment = null;

            var popup = Loc.GetString("mech-equipment-select-none-popup");

            if (_timing.IsFirstTimePredicted)
                _popup.PopupEntity(popup, uid, uid);

            if (_net.IsServer)
                Dirty(mechUid, mech);
            return;
        }

        var entity = GetEntity(ev.Equipment.Value);

        if (!mech.EquipmentContainer.Contains(entity))
            Log.Error("Mech does not have selected equipment");
        mech.CurrentSelectedEquipment = entity;

        var popupString = mech.CurrentSelectedEquipment != null
            ? Loc.GetString("mech-equipment-select-popup", ("item", mech.CurrentSelectedEquipment))
            : Loc.GetString("mech-equipment-select-none-popup");

        if (_timing.IsFirstTimePredicted)
            _popup.PopupEntity(popupString, uid, uid);

        if (_net.IsServer)
            Dirty(mechUid, mech);
    }

    public virtual void UpdateUserInterfaceByEquipment(EntityUid uid)
    {
    }
    protected void OnStorageInteract(EntityUid uid, MechComponent component, ref StorageInteractAttemptEvent args)
    {
        if (component.PilotSlot.ContainedEntity.HasValue)
            args.Cancelled = true;
    }

    private void ReceiveEquipmentUiMesssages<T>(EntityUid uid, MechComponent component, T args) where T : MechEquipmentUiMessage
    {
        if (!_timing.IsFirstTimePredicted)
            return;
        var ev = new MechEquipmentUiMessageRelayEvent(args, GetNetEntity(component.PilotSlot.ContainedEntity));
        var allEquipment = new List<EntityUid>(component.EquipmentContainer.ContainedEntities);
        var argEquip = GetEntity(args.Equipment);

        foreach (var equipment in allEquipment)
        {
            if (argEquip == equipment)
                RaiseLocalEvent(equipment, ev);
        }
    }
}
