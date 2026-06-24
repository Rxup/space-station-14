using System.Collections.Generic;
using Content.Server.Atmos.Components;
using Content.Server.Atmos.Rotting;
using Content.Shared.Atmos.Components;
using Content.Shared.Atmos.Rotting;
using Content.Shared.Backmen.Body.OrganRelations;
using Content.Shared.Backmen.Body.Systems;
using Content.Shared.Body;
using Content.Shared.Body.Organ;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Temperature.Components;
using Robust.Shared.Containers;
using Robust.Shared.Map;

namespace Content.Server.Backmen.Body;

public sealed partial class BkmDetachedBodySystem : EntitySystem
{
    [Dependency] private readonly Shared.Backmen.Body.OrganRelations.BkmDetachedBodySystem _detached = default!;
    [Dependency] private readonly BkmBodySharedSystem _body = default!;
    [Dependency] private readonly OrganRelationInitializerSystem _organRelations = default!;
    [Dependency] private readonly OrganRelationSystem _organRelation = default!;
    [Dependency] private readonly BkmDetachedBodyScatterSystem _scatter = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly RottingSystem _rotting = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BkmDetachedBodyComponent, EntInsertedIntoContainerMessage>(_detached.OnOrganInserted);
        SubscribeLocalEvent<BkmDetachedBodyComponent, EntRemovedFromContainerMessage>(_detached.OnOrganRemoved);

        SubscribeLocalEvent<BkmDetachedBodyComponent, BkmDetachedBodyCreatedEvent>(OnDetachedBodyCreated);
        SubscribeLocalEvent<BkmDetachedBodyComponent, GibDetachedBundleRequestEvent>(OnGibDetachedBundleRequest);
        SubscribeLocalEvent<BkmDetachedBodyComponent, DamageChangedEvent>(OnBundleShellDamaged);
        SubscribeLocalEvent<BkmDetachedBrainProtectionComponent, BeforeDamageChangedEvent>(OnBeforeBrainDamage);
        SubscribeLocalEvent<BkmDetachedBrainProtectionComponent, DamageModifyEvent>(OnBrainDamageModify);
        SubscribeLocalEvent<BkmDetachedBrainProtectionComponent, IsRottingEvent>(OnBrainIsRotting);
    }

    private void OnGibDetachedBundleRequest(Entity<BkmDetachedBodyComponent> ent, ref GibDetachedBundleRequestEvent args)
    {
        GibDetachedBundle(ent);
    }

    private void OnBundleShellDamaged(Entity<BkmDetachedBodyComponent> ent, ref DamageChangedEvent args)
    {
        if (args.DamageDelta == null || !args.DamageIncreased)
            return;

        if (!TryComp<BodyComponent>(ent, out var body) || body.Organs?.Count == 0)
            return;

        GibDetachedBundle(ent);
    }

    private void OnDetachedBodyCreated(Entity<BkmDetachedBodyComponent> ent, ref BkmDetachedBodyCreatedEvent args)
    {
        EnsureComp<DamageableComponent>(ent);

        var flammable = EnsureComp<FlammableComponent>(ent);
        flammable.Damage = new DamageSpecifier
        {
            DamageDict = new Dictionary<string, FixedPoint2> { { "Heat", 3 } }
        };

        EnsureComp<TemperatureComponent>(ent);
        EnsureComp<AtmosExposedComponent>(ent);

        if (TryComp<BodyComponent>(ent, out var body))
            _organRelations.WireRelationships((ent, body));

        _rotting.TransferRotToDetachedBody(args.SourceBody, ent);
    }

    private void OnBeforeBrainDamage(Entity<BkmDetachedBrainProtectionComponent> ent, ref BeforeDamageChangedEvent args)
    {
        args.Cancelled = true;
    }

    private void OnBrainDamageModify(Entity<BkmDetachedBrainProtectionComponent> ent, ref DamageModifyEvent args)
    {
        args.Damage = new DamageSpecifier();
    }

    private void OnBrainIsRotting(Entity<BkmDetachedBrainProtectionComponent> ent, ref IsRottingEvent args)
    {
        args.Handled = true;
    }

    /// <summary>
    /// Ejects contained organs when a detached bundle's root part is destroyed.
    /// </summary>
    public void GibDetachedBundle(Entity<BkmDetachedBodyComponent> bundle)
    {
        if (!TryComp<BodyComponent>(bundle, out var body)
            || !_containers.TryGetContainer(bundle, BodyComponent.ContainerID, out var organContainer)
            || organContainer.Count == 0)
            return;

        var origin = Transform(bundle).Coordinates;
        var root = bundle.Comp.RootOrgan;
        var toEject = new HashSet<EntityUid>();

        foreach (var organUid in organContainer.ContainedEntities)
        {
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
            EjectDetachedOrgan(organUid, origin);

            if (HasComp<BrainComponent>(organUid))
                EnsureComp<BkmDetachedBrainProtectionComponent>(organUid);
        }

        if (root is { } rootUid
            && !TerminatingOrDeleted(rootUid)
            && organContainer.Contains(rootUid)
            && TryComp<OrganComponent>(rootUid, out var rootOrgan))
        {
            _body.RemoveOrgan(rootUid, rootOrgan);
            QueueDel(rootUid);
        }

        if (organContainer.Count == 0)
            QueueDel(bundle);

        if (TryComp<DamageableComponent>(bundle, out var shell))
            _damageable.ClearAllDamage((bundle, shell));
    }

    private void EjectDetachedOrgan(EntityUid organ, EntityCoordinates origin)
    {
        _scatter.FlingViolentDetached(organ, origin);
    }
}
