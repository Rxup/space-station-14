using System.Linq;
using Content.Shared.Backmen.Body.Systems;
using Content.Shared.Backmen.Surgery.Tools;
using Content.Shared.Backmen.Surgery.Wounds.Components;
using Content.Shared.Backmen.Surgery.Wounds.Systems;
using Content.Shared.Backmen.Targeting;
using Content.Shared.Body;
using Content.Shared.Body.Part;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Examine;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.Backmen.Body.OrganRelations;

public sealed partial class BkmDetachedBodySystem : EntitySystem
{
    [Dependency] private BkmBodySharedSystem _body = default!;
    [Dependency] private OrganRelationSystem _organRelation = default!;
    [Dependency] private BkmDetachedBodyScatterSystem _scatter = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private ExamineSystemShared _examine = default!;
    [Dependency] private MetaDataSystem _metaData = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedTargetingSystem _targeting = default!;
    [Dependency] private WoundSystem _wounds = default!;
    [Dependency] private DamageableSystem _damageable = default!;

    private static readonly ResPath ScalpelIcon =
        new("/Textures/_Shitmed/Objects/Specific/Medical/Surgery/scalpel.rsi/scalpel.png");

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BkmDetachedBodyComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<BkmDetachedBodyComponent, GetVerbsEvent<ExamineVerb>>(OnGetVerbs);
        SubscribeLocalEvent<BkmDetachedBodyComponent, DamageDealtEvent>(OnDetachedBodyDamageDealt);
        SubscribeLocalEvent<BkmDetachedBodyComponent, GibDetachedBundleRequestEvent>(OnGibDetachedBundleRequest);
    }

    private void OnGibDetachedBundleRequest(Entity<BkmDetachedBodyComponent> ent, ref GibDetachedBundleRequestEvent args)
    {
        GibDetachedBundle(ent);
    }

    private void OnDetachedBodyDamageDealt(Entity<BkmDetachedBodyComponent> ent, ref DamageDealtEvent args)
    {
        if (!TryComp<BodyComponent>(ent, out var body) || body.Organs == null)
            return;

        var root = ent.Comp.RootOrgan;
        if (root is not { } rootOrgan || TerminatingOrDeleted(rootOrgan))
        {
            if (body.Organs.Count == 0)
            {
                if (_net.IsServer)
                    GibDetachedBundle(ent);

                return;
            }

            if (_net.IsServer)
                GibDetachedBundle(ent);

            return;
        }

        if (!_net.IsServer)
            return;

        DamageSpecifier actuallyInduced;
        ProtoId<DamageContainerPrototype>? container;

        if (TryComp<WoundableComponent>(rootOrgan, out var woundable))
        {
            actuallyInduced = _wounds.GetWoundsChanged((rootOrgan, woundable), args.Origin, args.Damage);
            container = woundable.DamageContainer;
        }
        else
        {
            actuallyInduced = args.Damage;
            container = null;
        }

        if (!TryComp<DamageableComponent>(rootOrgan, out var damageable))
            return;

        _damageable.ApplyDamageToDamageable(
            (rootOrgan, damageable),
            actuallyInduced,
            container,
            args.Origin,
            args.InterruptsDoAfters);
    }

    /// <summary>
    /// Ejects contained organs when a detached bundle's root part is destroyed.
    /// </summary>
    public void GibDetachedBundle(Entity<BkmDetachedBodyComponent> bundle)
    {
        if (!_net.IsServer)
            return;

        if (!TryComp<BodyComponent>(bundle, out var body)
            || !_containers.TryGetContainer(bundle, BodyComponent.ContainerID, out var organContainer))
            return;

        foreach (var organUid in organContainer.ContainedEntities.ToArray())
        {
            if (TerminatingOrDeleted(organUid) || EntityManager.IsQueuedForDeletion(organUid))
                _containers.Remove(organUid, organContainer, force: true);
        }

        if (organContainer.Count == 0)
        {
            QueueDel(bundle);
            return;
        }

        var origin = Transform(bundle).Coordinates;
        var root = bundle.Comp.RootOrgan;
        var toEject = new HashSet<EntityUid>();

        foreach (var organUid in organContainer.ContainedEntities)
        {
            if (TerminatingOrDeleted(organUid) || EntityManager.IsQueuedForDeletion(organUid))
                continue;

            if (root != null && organUid == root)
                continue;

            toEject.Add(organUid);
        }

        if (root is { } validRoot
            && !TerminatingOrDeleted(validRoot)
            && organContainer.Contains(validRoot))
        {
            foreach (var (organUid, _) in _body.GetOrgansForWoundable(validRoot))
                toEject.Add(organUid);

            foreach (var child in _organRelation.AllChildren(validRoot))
            {
                if (organContainer.Contains(child.Owner))
                    toEject.Add(child.Owner);
            }
        }

        foreach (var organUid in toEject)
        {
            if (!TryComp<OrganComponent>(organUid, out var organ))
                continue;

            _body.RemoveOrgan(organUid, organ);

            if (organContainer.Contains(organUid))
                _containers.Remove(organUid, organContainer, force: true);

            _scatter.FlingViolentDetached(organUid, origin);

            if (HasComp<BrainComponent>(organUid))
                EnsureComp<BkmDetachedBrainProtectionComponent>(organUid);
        }

        if (root is { } rootUid
            && !TerminatingOrDeleted(rootUid)
            && organContainer.Contains(rootUid)
            && TryComp<OrganComponent>(rootUid, out var rootOrgan))
        {
            _body.RemoveOrgan(rootUid, rootOrgan);

            if (organContainer.Contains(rootUid))
                _containers.Remove(rootUid, organContainer, force: true);

            QueueDel(rootUid);
        }

        foreach (var organUid in organContainer.ContainedEntities.ToArray())
            _containers.Remove(organUid, organContainer, force: true);

        if (!TerminatingOrDeleted(bundle) && !EntityManager.IsQueuedForDeletion(bundle))
            QueueDel(bundle);
    }

    /// <summary>
    /// Server-only. Called when an organ is inserted into a detached body bundle.
    /// </summary>
    public void OnOrganInserted(Entity<BkmDetachedBodyComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID != BodyComponent.ContainerID)
            return;

        if (!_net.IsServer || ent.Comp.RootOrgan != null)
            return;

        ent.Comp.RootOrgan = args.Entity;
        Dirty(ent, ent.Comp);
        UpdateMetadata(ent);
    }

    /// <summary>
    /// Server-only. Called when an organ is removed from a detached body bundle.
    /// </summary>
    public void OnOrganRemoved(Entity<BkmDetachedBodyComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID != BodyComponent.ContainerID)
            return;

        if (!_net.IsServer)
            return;

        if (ent.Comp.RootOrgan == args.Entity)
        {
            ent.Comp.RootOrgan = null;
            Dirty(ent, ent.Comp);
        }

        if (TryComp<BodyComponent>(ent, out var body)
            && body.Organs != null
            && body.Organs.Count == 0)
        {
            QueueDel(ent);
            return;
        }

        UpdateMetadata(ent);
    }

    private void UpdateMetadata(Entity<BkmDetachedBodyComponent> ent)
    {
        if (ent.Comp.RootOrgan is not { } root || TerminatingOrDeleted(root))
            return;

        var partName = Name(root);
        _metaData.SetEntityName(ent, partName);
        _metaData.SetEntityDescription(ent, Loc.GetString("bkm-detached-body-desc", ("part", partName)));
    }

    private void OnExamined(Entity<BkmDetachedBodyComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup(Loc.GetString("bkm-detached-body-examine-intro"));

        if (!TryComp<BodyComponent>(ent, out var body) || body.Organs == null)
            return;

        foreach (var organ in body.Organs.ContainedEntities)
        {
            args.PushMarkup(Loc.GetString("bkm-detached-body-examine-organ-line", ("organ", Name(organ))));
        }
    }

    private void OnGetVerbs(Entity<BkmDetachedBodyComponent> ent, ref GetVerbsEvent<ExamineVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess)
            return;

        if (!TryComp<BodyComponent>(ent, out var body)
            || body.Organs == null
            || body.Organs.ContainedEntities.Count == 0)
        {
            return;
        }

        var msg = FormattedMessage.FromMarkupOrThrow(Loc.GetString("bkm-detached-body-examine-header"));
        msg.PushNewline();

        foreach (var organ in body.Organs.ContainedEntities)
        {
            var organName = Name(organ);
            msg.AddMarkupPermissive(Loc.GetString("bkm-detached-body-examine-organ-header", ("organ", organName)));
            msg.PushNewline();

            if (TryComp<WoundableComponent>(organ, out var woundable) && woundable.IntegrityCap > 0)
            {
                var integrity = (woundable.WoundableIntegrity / woundable.IntegrityCap * 100).Float();
                msg.AddMarkupPermissive(Loc.GetString("bkm-detached-body-examine-integrity",
                    ("integrity", integrity.ToString("F0"))));
                msg.PushNewline();
            }

            var ev = new SurgeryToolExaminedEvent(msg);
            RaiseLocalEvent(organ, ref ev);
        }

        _examine.AddDetailedExamineVerb(args,
            ent.Comp,
            msg,
            Loc.GetString("bkm-detached-body-examinable-verb-text"),
            ScalpelIcon.CanonPath,
            Loc.GetString("bkm-detached-body-examinable-verb-message"));
    }

    /// <summary>
    /// Finds an organ inside a detached body bundle that matches the surgery part being reattached.
    /// </summary>
    public bool TryGetMatchingOrgan(
        EntityUid tool,
        BodyPartType part,
        BodyPartSymmetry? symmetry,
        out EntityUid organ)
    {
        organ = default;

        if (!TryComp<BkmDetachedBodyComponent>(tool, out _)
            || !TryComp<BodyComponent>(tool, out var body)
            || body.Organs == null)
        {
            return false;
        }

        foreach (var contained in body.Organs.ContainedEntities)
        {
            if (_targeting.MatchesBodyPartType(contained, part, symmetry))
            {
                organ = contained;
                return true;
            }
        }

        return false;
    }
}
