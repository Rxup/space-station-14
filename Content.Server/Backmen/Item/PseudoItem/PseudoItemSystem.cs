using Content.Shared.Verbs;
using Content.Shared.Item;
using Content.Shared.Hands;
using Content.Shared.DoAfter;
using Content.Shared.IdentityManagement;
using Content.Server.Storage.Components;
using Content.Server.Storage.EntitySystems;
using Content.Server.DoAfter;
using Content.Server.Item;
using Content.Server.Popups;
using Content.Server.Resist;
using Content.Shared.Backmen.Item;
using Content.Shared.Backmen.Item.PseudoItem;
using Content.Shared.Popups;
using Content.Shared.Resist;
using Content.Shared.Storage;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server.Backmen.Item.PseudoItem;

public sealed class PseudoItemSystem : SharedPseudoItemSystem
{
    [Dependency] private readonly StorageSystem _storageSystem = default!;
    [Dependency] private readonly ItemSystem _itemSystem = default!;
    [Dependency] private readonly TransformSystem _transformSystem = default!;
    [Dependency] private readonly PopupSystem _popup = default!;

    [Dependency] private readonly EntityStorageSystem _entityStorage = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PseudoItemComponent, EntGotRemovedFromContainerMessage>(OnEntRemoved);
        SubscribeLocalEvent<PseudoItemComponent, GettingPickedUpAttemptEvent>(OnGettingPickedUpAttempt);
        SubscribeLocalEvent<PseudoItemComponent, DropAttemptEvent>(OnDropAttempt);
        SubscribeLocalEvent<PseudoItemComponent, PseudoItemInsertDoAfterEvent>(OnDoAfter);

        SubscribeLocalEvent<PseudoItemComponent, EscapeInventoryEvent>(OnEscape, before: new[]{ typeof(EscapeInventorySystem) });
    }

    private void ClearState(EntityUid uid, PseudoItemComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        ClearState((uid,component));
    }

    private void ClearState(Entity<PseudoItemComponent> uid)
    {
        uid.Comp.Active = false;
        RemComp<ItemComponent>(uid);
        RemComp<CanEscapeInventoryComponent>(uid);
        _transformSystem.AttachToGridOrMap(uid);
    }

    private void OnEscape(Entity<PseudoItemComponent> uid, ref EscapeInventoryEvent args)
    {
        if (!TryComp<CanEscapeInventoryComponent>(uid, out var component))
        {
            return;
        }

        component.DoAfter = null;

        if (args.Handled || args.Cancelled)
            return;

        if (!uid.Comp.Active)
        {
            return;
        }

        args.Handled = true;

        if (args.Target.HasValue)
        {
            var parent = _transformSystem.GetParentUid(args.Target.Value);
            if (!parent.IsValid())
            {
                ClearState(uid);
                return;
            }



            if (CanInsertInto(uid, parent))
            {
                if(!TryInsert(parent, uid, uid))
                    return;
            }


            if (TryComp<EntityStorageComponent>(parent, out var entityStorageComponent))
            {
                ClearState(uid);
                if (_entityStorage.CanInsert(uid, parent, entityStorageComponent))
                {
                    _entityStorage.Insert(uid, parent, entityStorageComponent);
                    return;
                }
            }
        }


        ClearState(uid);
        //_containerSystem.AttachParentToContainerOrGrid(Transform(uid));
    }

    private void OnEntRemoved(EntityUid uid, PseudoItemComponent component, EntGotRemovedFromContainerMessage args)
    {
        if (!component.Active)
            return;

        ClearState(uid, component: component);
    }

    private void OnGettingPickedUpAttempt(EntityUid uid, PseudoItemComponent component, GettingPickedUpAttemptEvent args)
    {
        if (args.User == args.Item)
            return;

        ClearState(uid, component);
        args.Cancel();
    }

    private void OnDropAttempt(EntityUid uid, PseudoItemComponent component, DropAttemptEvent args)
    {
        if (component.Active)
            args.Cancel();
    }
    private void OnDoAfter(EntityUid uid, PseudoItemComponent component, DoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Args.Used == null)
            return;

        args.Handled = TryInsert(args.Args.Used.Value, (uid, component), args.User);
    }

    // ReSharper disable once MemberCanBePrivate.Global
    public override bool TryInsert(EntityUid storageUid, Entity<PseudoItemComponent> toInsert, EntityUid? user, StorageComponent? storage = null)
    {
        if (!Resolve(storageUid, ref storage))
            return false;

        if (!CanInsertInto(toInsert, storageUid, storage))
        {
            return false;
        }

        var item = EnsureComp<ItemComponent>(toInsert);
        _itemSystem.SetSize(toInsert, toInsert.Comp.Size, item);
        EnsureComp<CanEscapeInventoryComponent>(toInsert);

        if (!_storageSystem.Insert(storageUid, toInsert, out _, out var reason, user, storage))
        {
            if (!string.IsNullOrEmpty(reason) && user != null)
            {
                _popup.PopupEntity(Loc.GetString(reason),toInsert, user.Value, PopupType.LargeCaution);
            }
            ClearState(toInsert);
            return false;
        }

        _itemSystem.SetSize(toInsert, toInsert.Comp.SizeInBackpack, item);
        toInsert.Comp.Active = true;
        Dirty(toInsert);
        _transformSystem.AttachToGridOrMap(storageUid);
        return true;
    }
}
