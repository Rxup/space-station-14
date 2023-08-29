using System.Threading;
using Content.Shared.Verbs;
using Content.Shared.Item;
using Content.Shared.Hands;
using Content.Shared.DoAfter;
using Content.Shared.IdentityManagement;
using Content.Server.Storage.Components;
using Content.Server.Storage.EntitySystems;
using Content.Server.DoAfter;
using Content.Server.Item;
using Content.Server.Resist;
using Content.Shared.Backmen.Item;
using Robust.Shared.Containers;

namespace Content.Server.Backmen.Item.PseudoItem
{
    public sealed class PseudoItemSystem : EntitySystem
    {
        [Dependency] private readonly StorageSystem _storageSystem = default!;
        [Dependency] private readonly ItemSystem _itemSystem = default!;
        [Dependency] private readonly DoAfterSystem _doAfter = default!;
        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<PseudoItemComponent, GetVerbsEvent<InnateVerb>>(AddInsertVerb);
            SubscribeLocalEvent<PseudoItemComponent, GetVerbsEvent<AlternativeVerb>>(AddInsertAltVerb);
            SubscribeLocalEvent<PseudoItemComponent, EntGotRemovedFromContainerMessage>(OnEntRemoved);
            SubscribeLocalEvent<PseudoItemComponent, GettingPickedUpAttemptEvent>(OnGettingPickedUpAttempt);
            SubscribeLocalEvent<PseudoItemComponent, DropAttemptEvent>(OnDropAttempt);
            SubscribeLocalEvent<PseudoItemComponent, PseudoItemInsertDoAfterEvent>(OnDoAfter);
        }

        private void AddInsertVerb(EntityUid uid, PseudoItemComponent component, GetVerbsEvent<InnateVerb> args)
        {
            if (!args.CanInteract || !args.CanAccess)
                return;

            if (component.Active)
                return;

            if (!TryComp<ServerStorageComponent>(args.Target, out var targetStorage))
                return;

            if (component.Size > targetStorage.StorageCapacityMax - targetStorage.StorageUsed)
                return;

            if (Transform(args.Target).ParentUid == uid)
                return;

            InnateVerb verb = new()
            {
                Act = () =>
                {
                    TryInsert(args.Target, uid, component, targetStorage);
                },
                Text = Loc.GetString("action-name-insert-self"),
                Priority = 2
            };
            args.Verbs.Add(verb);
        }

        private void AddInsertAltVerb(EntityUid uid, PseudoItemComponent component, GetVerbsEvent<AlternativeVerb> args)
        {
            if (!args.CanInteract || !args.CanAccess)
                return;

            if (args.User == args.Target)
                return;

            if (args.Hands == null)
                return;

            if (!TryComp<ServerStorageComponent>(args.Hands.ActiveHandEntity, out var targetStorage))
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

        private void OnEntRemoved(EntityUid uid, PseudoItemComponent component, EntGotRemovedFromContainerMessage args)
        {
            if (!component.Active)
                return;

            RemComp<ItemComponent>(uid);
            RemComp<CanEscapeInventoryComponent>(uid);
            component.Active = false;
        }

        private void OnGettingPickedUpAttempt(EntityUid uid, PseudoItemComponent component, GettingPickedUpAttemptEvent args)
        {
            if (args.User == args.Item)
                return;

            Transform(uid).AttachToGridOrMap();
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

            args.Handled = TryInsert(args.Args.Used.Value, uid, component);
        }

        public bool TryInsert(EntityUid storageUid, EntityUid toInsert, PseudoItemComponent component, ServerStorageComponent? storage = null)
        {
            if (!Resolve(storageUid, ref storage))
                return false;

            if (component.Size > storage.StorageCapacityMax - storage.StorageUsed)
                return false;

            var item = EnsureComp<ItemComponent>(toInsert);
            _itemSystem.SetSize(toInsert, component.Size, item);

            EnsureComp<CanEscapeInventoryComponent>(toInsert);

            if (!_storageSystem.Insert(storageUid, toInsert, storage))
            {
                component.Active = false;
                RemComp<ItemComponent>(toInsert);
                RemComp<CanEscapeInventoryComponent>(toInsert);
                return false;
            } else
            {
                component.Active = true;
                Transform(storageUid).AttachToGridOrMap();
                return true;
            }
        }
        private void StartInsertDoAfter(EntityUid inserter, EntityUid toInsert, EntityUid storageEntity, PseudoItemComponent? pseudoItem = null)
        {
            if (!Resolve(toInsert, ref pseudoItem))
                return;

            var ev = new PseudoItemInsertDoAfterEvent();
            var args = new DoAfterArgs(inserter, 5f, ev, toInsert, target: toInsert, used: storageEntity)
            {
                BreakOnTargetMove = true,
                BreakOnUserMove = true,
                NeedHand = true
            };

            _doAfter.TryStartDoAfter(args);
        }
    }
}
