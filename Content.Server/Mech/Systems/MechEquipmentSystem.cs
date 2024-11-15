using Content.Server.Mech.Equipment.Components;
using Content.Server.Popups;
using Content.Shared.ADT.Mech.EntitySystems;
using Content.Shared.ADT.Weapons.Ranged.Components;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Mech;
using Content.Shared.Mech.Components;
using Content.Shared.Mech.Equipment.Components;
using Content.Shared.Whitelist;

namespace Content.Server.Mech.Systems;

/// <summary>
/// Handles the insertion of mech equipment into mechs.
/// </summary>
public sealed class MechEquipmentSystem : SharedMechEquipmentSystem // ADT - Parent changed
{
    [Dependency] private readonly MechSystem _mech = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelistSystem = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();  // ADT fix I guess?

        SubscribeLocalEvent<MechEquipmentComponent, AfterInteractEvent>(OnUsed);
        SubscribeLocalEvent<MechEquipmentComponent, InsertEquipmentEvent>(OnInsertEquipment);

        // ADT Content start
        SubscribeLocalEvent<MechEquipmentComponent, EntityTerminatingEvent>(OnTerminating);
        SubscribeLocalEvent<MechEquipmentComponent, MechEquipmentUiStateReadyEvent>(OnGetUIState);
        // ADT Content end
    }

    private void OnUsed(EntityUid uid, MechEquipmentComponent component, AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target == null)
            return;

        var mech = args.Target.Value;
        if (!TryComp<MechComponent>(mech, out var mechComp))
            return;

        if (mechComp.Broken)
            return;

        if (args.User == mechComp.PilotSlot.ContainedEntity)
            return;

        if (mechComp.EquipmentContainer.ContainedEntities.Count >= mechComp.MaxEquipmentAmount)
            return;

        if (_whitelistSystem.IsWhitelistFail(mechComp.EquipmentWhitelist, args.Used))
            return;

        _popup.PopupEntity(Loc.GetString("mech-equipment-begin-install", ("item", uid)), mech);

        var doAfterEventArgs = new DoAfterArgs(EntityManager, args.User, component.InstallDuration, new InsertEquipmentEvent(), uid, target: mech, used: uid)
        {
            BreakOnMove = true,
        };

        _doAfter.TryStartDoAfter(doAfterEventArgs);
    }

    private void OnInsertEquipment(EntityUid uid, MechEquipmentComponent component, InsertEquipmentEvent args)
    {
        if (args.Handled || args.Cancelled || args.Args.Target == null)
            return;

        _popup.PopupEntity(Loc.GetString("mech-equipment-finish-install", ("item", uid)), args.Args.Target.Value);
        _mech.InsertEquipment(args.Args.Target.Value, uid);

        args.Handled = true;
    }

    // ADT Content start
    private void OnTerminating(EntityUid uid, MechEquipmentComponent comp, ref EntityTerminatingEvent args)
    {
        _mech.UpdateUserInterfaceByEquipment(uid);
    }

    private void OnGetUIState(EntityUid uid, MechEquipmentComponent component, MechEquipmentUiStateReadyEvent args)
    {
        if (HasComp<MechGrabberComponent>(uid)) // Мне лень делать нормальную проверку, как-нибудь потом будет.
            return;
        if (HasComp<BallisticMechAmmoProviderComponent>(uid))
            return;

        args.States.Add(GetNetEntity(uid), null);
    }
    // ADT Content end
}
