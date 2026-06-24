using System.Numerics;
using Content.Server.Atmos.Components;
using Content.Server.Atmos.Rotting;
using Content.Shared.Atmos.Components;
using Content.Shared.Atmos.Rotting;
using Content.Shared.Backmen.Body.OrganRelations;
using Content.Shared.Backmen.Body.Systems;
using Content.Shared.Backmen.Surgery.Wounds;
using Content.Shared.Backmen.Surgery.Wounds.Components;
using Content.Shared.Body;
using Content.Shared.Body.Organ;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Temperature.Components;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Backmen.Body;

public sealed partial class BkmDetachedBodySystem : EntitySystem
{
    [Dependency] private readonly Shared.Backmen.Body.OrganRelations.BkmDetachedBodySystem _detached = default!;
    [Dependency] private readonly BkmBodySharedSystem _body = default!;
    [Dependency] private readonly OrganRelationSystem _organRelation = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly BkmDetachedBodyScatterSystem _scatter = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;
    [Dependency] private readonly RottingSystem _rotting = default!;

    private EntityUid? _relayingBundleDamage;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BkmDetachedBodyComponent, EntInsertedIntoContainerMessage>(_detached.OnOrganInserted);
        SubscribeLocalEvent<BkmDetachedBodyComponent, EntRemovedFromContainerMessage>(_detached.OnOrganRemoved);

        SubscribeLocalEvent<BkmDetachedBodyComponent, BkmDetachedBodyCreatedEvent>(OnDetachedBodyCreated);
        SubscribeLocalEvent<BkmDetachedBodyComponent, GibDetachedBundleRequestEvent>(OnGibDetachedBundleRequest);
        SubscribeLocalEvent<BkmDetachedBodyComponent, BeforeDamageChangedEvent>(OnBeforeBundleDamage);
        SubscribeLocalEvent<BkmDetachedBrainProtectionComponent, BeforeDamageChangedEvent>(OnBeforeBrainDamage);
        SubscribeLocalEvent<BkmDetachedBrainProtectionComponent, DamageModifyEvent>(OnBrainDamageModify);
        SubscribeLocalEvent<BkmDetachedBrainProtectionComponent, IsRottingEvent>(OnBrainIsRotting);
    }

    private void OnGibDetachedBundleRequest(Entity<BkmDetachedBodyComponent> ent, ref GibDetachedBundleRequestEvent args)
    {
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

        _rotting.TransferRotToDetachedBody(args.SourceBody, ent);
    }

    private void OnBeforeBundleDamage(Entity<BkmDetachedBodyComponent> ent, ref BeforeDamageChangedEvent args)
    {
        if (_relayingBundleDamage != null
            || args.Cancelled
            || ent.Comp.RootOrgan is not { } root
            || TerminatingOrDeleted(root))
            return;

        args.Cancelled = true;
        _relayingBundleDamage = ent;
        _damageable.ChangeDamage(root, args.Damage, origin: args.Origin);
        _relayingBundleDamage = null;
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
            || !_containers.TryGetContainer(bundle, BodyComponent.ContainerID, out var organContainer))
            return;

        if (bundle.Comp.RootOrgan is not { } root || !organContainer.Contains(root))
            return;

        var origin = Transform(bundle).Coordinates;
        var toEject = new HashSet<EntityUid>();

        foreach (var (organUid, _) in _body.GetOrgansForWoundable(root))
            toEject.Add(organUid);

        foreach (var child in _organRelation.AllChildren(root))
        {
            if (organContainer.Contains(child.Owner))
                toEject.Add(child.Owner);
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

        if (TryComp<OrganComponent>(root, out var rootOrgan))
            _body.RemoveOrgan(root, rootOrgan);

        if (!TerminatingOrDeleted(root))
            QueueDel(root);

        if (organContainer.Count == 0)
            QueueDel(bundle);
    }

    private void EjectDetachedOrgan(EntityUid organ, EntityCoordinates origin)
    {
        _transform.SetCoordinates(organ, origin);
        _transform.AttachToGridOrMap(organ);

        var distance = _random.NextFloat(
            BkmDetachedBodyScatterSystem.ViolentScatterMin,
            BkmDetachedBodyScatterSystem.ViolentScatterMax);
        var world = _transform.ToMapCoordinates(origin).Position + _random.NextAngle().ToVec() * distance;
        _transform.SetWorldPosition(organ, world);

        if (TryComp(organ, out PhysicsComponent? physics) && physics.BodyType != BodyType.Static)
        {
            var impulse = _random.NextAngle().ToVec()
                * (BkmDetachedBodyScatterSystem.ViolentFlingImpulse * BkmDetachedBodyScatterSystem.ViolentFlingImpulseMultiplier);
            _physics.ApplyLinearImpulse(organ, impulse, body: physics);
        }
    }
}
