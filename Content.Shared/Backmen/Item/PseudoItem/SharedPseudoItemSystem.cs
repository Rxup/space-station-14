using Content.Shared.DoAfter;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction.Components;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Shared.Backmen.Item.PseudoItem;

public abstract class SharedPseudoItemSystem : EntitySystem
{
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly SharedStorageSystem _storageSystem = default!;

    protected EntityQuery<StorageComponent> StorageQuery;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PseudoItemComponent, GetVerbsEvent<InnateVerb>>(AddInsertVerb);
        SubscribeLocalEvent<PseudoItemComponent, GetVerbsEvent<AlternativeVerb>>(AddInsertAltVerb);
        SubscribeLocalEvent<PseudoItemComponent, ContainerGettingRemovedAttemptEvent>(OnRemovedAttempt);

        StorageQuery = GetEntityQuery<StorageComponent>();
    }

    private void OnRemovedAttempt(EntityUid uid, PseudoItemComponent component, ContainerGettingRemovedAttemptEvent args)
    {
        if (
            uid != args.EntityUid &&
            StorageQuery.HasComponent(args.Container.Owner) &&
            !TerminatingOrDeleted(args.Container.Owner) &&
            !EntityManager.IsQueuedForDeletion(args.Container.Owner)
        )
        {
            args.Cancel();
        }
    }

    public abstract bool TryInsert(EntityUid storageUid, Entity<PseudoItemComponent> toInsert, EntityUid? user, StorageComponent? storage = null);

    private void AddInsertVerb(EntityUid uid, PseudoItemComponent component, GetVerbsEvent<InnateVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess)
            return;

        if (component.Active)
            return;

        if(!StorageQuery.TryGetComponent(args.Target, out var storage))
            return;

        if (Transform(args.Target).ParentUid == uid)
            return;

        InnateVerb verb = new()
        {
            Act = () =>
            {
                TryInsert(args.Target, (uid, component), uid, storage);
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

        if (args.Hands?.ActiveHandEntity == null)
            return;

        if (!StorageQuery.HasComponent(args.Hands.ActiveHandEntity))
            return;

        AlternativeVerb verb = new()
        {
            Act = () =>
            {
                StartInsertDoAfter(args.User, uid, args.Hands.ActiveHandEntity.Value, component);
            },
            Text = Loc.GetString("action-name-insert-other", ("target", args.Target)),
            Priority = 2
        };
        args.Verbs.Add(verb);
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

    protected bool CanInsertInto(Entity<PseudoItemComponent> pseudoItem, EntityUid storage, StorageComponent? storageComponent = null)
    {
        if (HasComp<UnremoveableComponent>(storage))
        {
            return false;
        }
        if (!Resolve(storage, ref storageComponent, false))
        {
            return false;
        }

        if (!_prototypeManager.TryIndex(pseudoItem.Comp.SizeInBackpack, out var itemSizeInBackpack))
        {
            return false;
        }

        return itemSizeInBackpack.Weight <= storageComponent.Grid.GetArea() - _storageSystem.GetCumulativeItemAreas((storage,storageComponent));
    }
}
