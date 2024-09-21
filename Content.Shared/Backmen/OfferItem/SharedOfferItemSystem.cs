using Content.Shared.Alert;
using Content.Shared.Backmen.Alert.Click;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Robust.Shared.Player;

namespace Content.Shared.Backmen.OfferItem;

public abstract partial class SharedOfferItemSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<OfferItemComponent, AfterInteractUsingEvent>(SetInReceiveMode);
        SubscribeLocalEvent<OfferItemComponent, MoveEvent>(OnMove);

        InitializeInteractions();

        SubscribeAllEvent<AcceptOfferAlertEvent>(OnClickAlertEvent);
    }

    [ValidatePrototypeId<AlertPrototype>]
    protected const string OfferAlert = "Offer";
    private void OnClickAlertEvent(AcceptOfferAlertEvent ev)
    {
        if(ev.Handled)
            return;
        if (ev.AlertId != OfferAlert)
        {
            return;
        }

        if (!TryComp<OfferItemComponent>(ev.User, out var itemComponent))
        {
            return;
        }
        ev.Handled = true;

        Receive(ev.User, itemComponent);
    }
    /// <summary>
    /// Accepting the offer and receive item
    /// </summary>
    public void Receive(EntityUid uid, OfferItemComponent? component = null)
    {
        if (!Resolve(uid, ref component) ||
            !TryComp<OfferItemComponent>(component.Target, out var offerItem) ||
            offerItem.Hand == null ||
            component.Target == null ||
            !TryComp<HandsComponent>(uid, out var hands))
            return;

        if (offerItem.Item != null)
        {
            if (!_hands.TryPickup(uid, offerItem.Item.Value, handsComp: hands))
            {
                _popup.PopupEntity(Loc.GetString("offer-item-full-hand"), uid, uid);
                return;
            }

            _popup.PopupEntity(Loc.GetString("offer-item-give",
                ("item", Identity.Entity(offerItem.Item.Value, EntityManager)),
                ("target", Identity.Entity(uid, EntityManager))), component.Target.Value, component.Target.Value);
            _popup.PopupEntity(Loc.GetString("offer-item-give-other",
                    ("user", Identity.Entity(component.Target.Value, EntityManager)),
                    ("item", Identity.Entity(offerItem.Item.Value, EntityManager)),
                    ("target", Identity.Entity(uid, EntityManager)))
                , component.Target.Value, Filter.PvsExcept(component.Target.Value, entityManager: EntityManager), true);
        }

        offerItem.Item = null;
        Dirty(uid,offerItem);
        UnReceive(uid, component, offerItem);
    }

    private void SetInReceiveMode(EntityUid uid, OfferItemComponent component, AfterInteractUsingEvent args)
    {
        if (!TryComp<OfferItemComponent>(args.User, out var offerItem))
            return;

        if (args.User == uid || component.IsInReceiveMode ||
            (offerItem.IsInReceiveMode && offerItem.Target != uid))
            return;

        component.IsInReceiveMode = true;
        component.Target = args.User;

        Dirty(uid, component);

        offerItem.Target = uid;
        offerItem.IsInOfferMode = false;

        Dirty(args.User, offerItem);

        if (offerItem.Item == null)
            return;

        _popup.PopupEntity(Loc.GetString("offer-item-try-give",
            ("item", Identity.Entity(offerItem.Item.Value, EntityManager)),
            ("target", Identity.Entity(uid, EntityManager))), component.Target.Value, component.Target.Value);
        _popup.PopupEntity(Loc.GetString("offer-item-try-give-target",
            ("user", Identity.Entity(component.Target.Value, EntityManager)),
            ("item", Identity.Entity(offerItem.Item.Value, EntityManager))), component.Target.Value, uid);
    }

    private void OnMove(EntityUid uid, OfferItemComponent component, MoveEvent args)
    {
        if (component.Target == null ||
            _transform.InRange(args.NewPosition,
                Transform(component.Target.Value).Coordinates,
                component.MaxOfferDistance)
            )
            return;

        UnOffer(uid, component);
    }

    /// <summary>
    /// Resets the <see cref="OfferItemComponent"/> of the user and the target
    /// </summary>
    protected void UnOffer(EntityUid uid, OfferItemComponent component)
    {
        if (!TryComp<HandsComponent>(uid, out var hands) || hands.ActiveHand == null)
            return;


        if (TryComp<OfferItemComponent>(component.Target, out var offerItem) && component.Target != null)
        {

            if (component.Item != null)
            {
                _popup.PopupEntity(Loc.GetString("offer-item-no-give",
                    ("item", Identity.Entity(component.Item.Value, EntityManager)),
                    ("target", Identity.Entity(component.Target.Value, EntityManager))), uid, uid);
                _popup.PopupEntity(Loc.GetString("offer-item-no-give-target",
                    ("user", Identity.Entity(uid, EntityManager)),
                    ("item", Identity.Entity(component.Item.Value, EntityManager))), uid, component.Target.Value);
            }

            else if (offerItem.Item != null)
            {
                _popup.PopupEntity(Loc.GetString("offer-item-no-give",
                    ("item", Identity.Entity(offerItem.Item.Value, EntityManager)),
                    ("target", Identity.Entity(uid, EntityManager))), component.Target.Value, component.Target.Value);
                _popup.PopupEntity(Loc.GetString("offer-item-no-give-target",
                    ("user", Identity.Entity(component.Target.Value, EntityManager)),
                    ("item", Identity.Entity(offerItem.Item.Value, EntityManager))), component.Target.Value, uid);
            }

            offerItem.IsInOfferMode = false;
            offerItem.IsInReceiveMode = false;
            offerItem.Hand = null;
            offerItem.Target = null;
            offerItem.Item = null;

            Dirty(component.Target.Value, offerItem);
        }

        component.IsInOfferMode = false;
        component.IsInReceiveMode = false;
        component.Hand = null;
        component.Target = null;
        component.Item = null;

        Dirty(uid, component);
    }


    /// <summary>
    /// Cancels the transfer of the item
    /// </summary>
    protected void UnReceive(EntityUid uid, OfferItemComponent? component = null, OfferItemComponent? offerItem = null)
    {
        if (component == null && !TryComp(uid, out component))
            return;

        if (offerItem == null && !TryComp(component.Target, out offerItem))
            return;

        if (!TryComp<HandsComponent>(uid, out var hands) || hands.ActiveHand == null ||
            component.Target == null)
            return;

        if (offerItem.Item != null)
        {
            _popup.PopupEntity(Loc.GetString("offer-item-no-give",
                ("item", Identity.Entity(offerItem.Item.Value, EntityManager)),
                ("target", Identity.Entity(uid, EntityManager))), component.Target.Value, component.Target.Value);
            _popup.PopupEntity(Loc.GetString("offer-item-no-give-target",
                ("user", Identity.Entity(component.Target.Value, EntityManager)),
                ("item", Identity.Entity(offerItem.Item.Value, EntityManager))), component.Target.Value, uid);
        }

        if (!offerItem.IsInReceiveMode)
        {
            offerItem.Target = null;
            component.Target = null;
        }

        offerItem.Item = null;
        offerItem.Hand = null;
        component.IsInReceiveMode = false;

        Dirty(uid, component);
    }

    /// <summary>
    /// Returns true if <see cref="OfferItemComponent.IsInOfferMode"/> = true
    /// </summary>
    protected bool IsInOfferMode(EntityUid? entity, OfferItemComponent? component = null)
    {
        return entity != null && Resolve(entity.Value, ref component, false) && component.IsInOfferMode;
    }
}
