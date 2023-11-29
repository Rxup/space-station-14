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
using Content.Shared.Popups;
using Content.Shared.Resist;
using Content.Shared.Storage;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server.Backmen.Item.PseudoItem
{
    public sealed class PseudoItemSystem : EntitySystem
    {
        [Dependency] private readonly StorageSystem _storageSystem = default!;
        [Dependency] private readonly ItemSystem _itemSystem = default!;
        [Dependency] private readonly DoAfterSystem _doAfter = default!;
        [Dependency] private readonly TransformSystem _transformSystem = default!;
        [Dependency] private readonly PopupSystem _popup = default!;
        [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
        [Dependency] private readonly EntityStorageSystem _entityStorage = default!;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<PseudoItemComponent, GetVerbsEvent<InnateVerb>>(AddInsertVerb);
            SubscribeLocalEvent<PseudoItemComponent, GetVerbsEvent<AlternativeVerb>>(AddInsertAltVerb);
            SubscribeLocalEvent<PseudoItemComponent, EntGotRemovedFromContainerMessage>(OnEntRemoved);
            SubscribeLocalEvent<PseudoItemComponent, GettingPickedUpAttemptEvent>(OnGettingPickedUpAttempt);
            SubscribeLocalEvent<PseudoItemComponent, DropAttemptEvent>(OnDropAttempt);
            SubscribeLocalEvent<PseudoItemComponent, PseudoItemInsertDoAfterEvent>(OnDoAfter);
            SubscribeLocalEvent<PseudoItemComponent, ContainerGettingRemovedAttemptEvent>(OnRemovedAttempt);
            SubscribeLocalEvent<PseudoItemComponent, EscapeInventoryEvent>(OnEscape, before: new[]{ typeof(EscapeInventorySystem) });
        }

        private void OnRemovedAttempt(EntityUid uid, PseudoItemComponent component, ContainerGettingRemovedAttemptEvent args)
        {
            if (
                HasComp<StorageComponent>(args.Container.Owner) &&
                !TerminatingOrDeleted(args.Container.Owner) &&
                !EntityManager.IsQueuedForDeletion(args.Container.Owner)
                )
            {
                args.Cancel();
            }
        }

        private void AddInsertVerb(EntityUid uid, PseudoItemComponent component, GetVerbsEvent<InnateVerb> args)
        {
            if (!args.CanInteract || !args.CanAccess)
                return;

            if (component.Active)
                return;

            if (!TryComp<StorageComponent>(args.Target, out var storage))
                return;

            if (Transform(args.Target).ParentUid == uid)
                return;

            InnateVerb verb = new()
            {
                Act = () =>
                {
                    TryInsert(args.Target, uid, uid, component, storage);
                },
                Text = Loc.GetString("action-name-insert-self"),
                Priority = 2
            };
            args.Verbs.Add(verb);
        }

        private void ClearState(EntityUid uid, PseudoItemComponent? component = null)
        {
            if (!Resolve(uid, ref component))
                return;

            component.Active = false;
            RemComp<ItemComponent>(uid);
            RemComp<CanEscapeInventoryComponent>(uid);
            _transformSystem.AttachToGridOrMap(uid);
        }

        private void AddInsertAltVerb(EntityUid uid, PseudoItemComponent component, GetVerbsEvent<AlternativeVerb> args)
        {
            if (!args.CanInteract || !args.CanAccess)
                return;

            if (args.User == args.Target)
                return;

            if (args.Hands == null)
                return;

            if (!HasComp<StorageComponent>(args.Hands.ActiveHandEntity))
                return;

            AlternativeVerb verb = new()
            {
                Act = () =>
                {
                    StartInsertDoAfter(args.User, uid, args.Hands.ActiveHandEntity.Value, component);
                },
                Text = Loc.GetString("action-name-insert-other", ("target", Identity.Entity(args.Target, EntityManager))),
                Priority = 2
            };
            args.Verbs.Add(verb);
        }

        private bool CanInsertInto(Entity<PseudoItemComponent> pseudoItem, EntityUid storage, StorageComponent? storageComponent = null)
        {
            if (!Resolve(storage, ref storageComponent, false))
            {
                return false;
            }

            if (storageComponent.MaxSlots != null)
            {
                return false;
            }

            if (!_prototypeManager.TryIndex(pseudoItem.Comp.SizeInBackpack, out var sizeInBackpack))
            {
                return false;
            }

            return sizeInBackpack.Weight >= storageComponent.MaxTotalWeight - _storageSystem.GetCumulativeItemSizes(storage, storageComponent);
        }

        private void OnEscape(EntityUid uid, PseudoItemComponent pseudoItem, EscapeInventoryEvent args)
        {
            if (!TryComp<CanEscapeInventoryComponent>(uid, out var component))
            {
                return;
            }

            component.DoAfter = null;
            Dirty(uid, component);

            if (args.Handled || args.Cancelled)
                return;

            if (!pseudoItem.Active)
            {
                return;
            }

            args.Handled = true;

            if (args.Target.HasValue)
            {
                var parent = _transformSystem.GetParentUid(args.Target.Value);
                if (!parent.IsValid())
                {
                    ClearState(uid, pseudoItem);
                    return;
                }



                if (CanInsertInto((uid, pseudoItem), parent))
                {
                    if(!TryInsert(parent, uid, uid, pseudoItem))
                        return;
                }


                if (TryComp<EntityStorageComponent>(parent, out var entityStorageComponent))
                {
                    ClearState(uid, pseudoItem);
                    if (_entityStorage.CanInsert(uid, parent, entityStorageComponent))
                    {
                        _entityStorage.Insert(uid, parent, entityStorageComponent);
                        return;
                    }
                }
            }


            ClearState(uid, pseudoItem);
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

            args.Handled = TryInsert(args.Args.Used.Value, uid, args.User, component);
        }

        // ReSharper disable once MemberCanBePrivate.Global
        public bool TryInsert(EntityUid storageUid, EntityUid toInsert, EntityUid? user, PseudoItemComponent component, StorageComponent? storage = null)
        {
            if (!Resolve(storageUid, ref storage))
                return false;

            if (!CanInsertInto((toInsert, component), storageUid, storage))
            {
                if (user.HasValue)
                {
                    _popup.PopupEntity(
                        storage.MaxSlots == null
                            ? Loc.GetString("comp-storage-too-big")
                            : Loc.GetString("comp-storage-invalid-container"), toInsert, user.Value,
                        PopupType.LargeCaution);
                }
                return false;
            }


            var item = EnsureComp<ItemComponent>(toInsert);
            _itemSystem.SetSize(toInsert, component.Size, item);
            EnsureComp<CanEscapeInventoryComponent>(toInsert);

            if (!_storageSystem.Insert(storageUid, toInsert, out _, out var reason, user, storage))
            {
                if (!string.IsNullOrEmpty(reason) && user != null)
                {
                    _popup.PopupEntity(Loc.GetString(reason),toInsert, user.Value, PopupType.LargeCaution);
                }
                ClearState(toInsert, component);
                return false;
            }

            _itemSystem.SetSize(toInsert, component.SizeInBackpack, item);
            component.Active = true;
            _transformSystem.AttachToGridOrMap(storageUid);
            return true;
        }
        private void StartInsertDoAfter(EntityUid inserter, EntityUid toInsert, EntityUid storageEntity, PseudoItemComponent? pseudoItem = null)
        {
            if (!Resolve(toInsert, ref pseudoItem))
                return;

            var ev = new PseudoItemInsertDoAfterEvent();
            var args = new DoAfterArgs(EntityManager, inserter, 5f, ev, toInsert, target: toInsert, used: storageEntity)
            {
                BreakOnTargetMove = true,
                BreakOnUserMove = true,
                NeedHand = true
            };

            _doAfter.TryStartDoAfter(args);
        }
    }
}
