using Content.Server.Crayon;
using Content.Server.Popups;
using Content.Server._Impstation.Revenant.Components;
using Content.Server.Revenant.EntitySystems;
using Content.Shared.Hands;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Item;
using Content.Shared.Revenant.Components;

namespace Content.Server._Impstation.Revenant.EntitySystems;

public sealed partial class BloodCrayonSystem : EntitySystem
{
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private RevenantSystem _revenant = default!;
    [Dependency] private SharedHandsSystem _hands = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BloodCrayonComponent, AfterInteractEvent>(OnCrayonUse, before: [typeof(CrayonSystem)]);
        SubscribeLocalEvent<BloodCrayonComponent, GotUnequippedHandEvent>(OnUnequipped);
        SubscribeLocalEvent<BloodCrayonComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<RevenantComponent, PickupAttemptEvent>(OnRevenantPickup);
    }

    private void OnCrayonUse(EntityUid uid, BloodCrayonComponent comp, AfterInteractEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<RevenantComponent>(args.User, out var revenant))
            return;

        if (!_revenant.ChangeEssenceAmount(args.User, -revenant.BloodWritingCost, allowDeath: false))
        {
            _popup.PopupEntity(Loc.GetString("revenant-not-enough-essence"), uid, args.User);
            args.Handled = true;
        }
    }

    private void OnRevenantPickup(EntityUid uid, RevenantComponent component, PickupAttemptEvent args)
    {
        if (!HasComp<BloodCrayonComponent>(args.Item))
            args.Cancel();
    }

    private void OnUnequipped(EntityUid uid, BloodCrayonComponent comp, GotUnequippedHandEvent args)
    {
        if (TryComp<RevenantComponent>(args.User, out var revenant))
            revenant.BloodCrayon = null;

        if (!TerminatingOrDeleted(uid))
            QueueDel(uid);
    }

    private void OnShutdown(EntityUid uid, BloodCrayonComponent comp, ComponentShutdown args)
    {
        if (comp.Revenant is not { } revenant)
            return;

        if (TryComp<RevenantComponent>(revenant, out var revComp))
            revComp.BloodCrayon = null;

        if (!TryComp<HandsComponent>(revenant, out var hands))
            return;

        if (_hands.TryGetHand((revenant, hands), RevenantComponent.BloodWritingHand, out _))
            _hands.RemoveHand((revenant, hands), RevenantComponent.BloodWritingHand);
    }
}
