using Content.Server.Atmos.Components;
using Content.Server.Atmos.Rotting;
using Content.Shared.Atmos.Components;
using Content.Shared.Atmos.Rotting;
using Content.Shared.Backmen.Damage;
using Content.Shared.Backmen.Body.OrganRelations;
using Content.Shared.Body;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Temperature.Components;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server.Backmen.Body;

public sealed partial class BkmDetachedBodySystem : EntitySystem
{
    [Dependency] private Shared.Backmen.Body.OrganRelations.BkmDetachedBodySystem _detached = default!;
    [Dependency] private OrganRelationInitializerSystem _organRelations = default!;
    [Dependency] private RottingSystem _rotting = default!;
    [Dependency] private BackmenDamageModelExclusivitySystem _backmenDamageExclusivity = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BkmDetachedBodyComponent, MapInitEvent>(OnDetachedBodyMapInit);
        SubscribeLocalEvent<BkmDetachedBodyComponent, EntInsertedIntoContainerMessage>(_detached.OnOrganInserted);
        SubscribeLocalEvent<BkmDetachedBodyComponent, EntRemovedFromContainerMessage>(_detached.OnOrganRemoved);

        SubscribeLocalEvent<BkmDetachedBodyComponent, BkmDetachedBodyCreatedEvent>(OnDetachedBodyCreated);
        SubscribeLocalEvent<BkmDetachedBrainProtectionComponent, BeforeDamageChangedEvent>(OnBeforeBrainDamage);
        SubscribeLocalEvent<BkmDetachedBrainProtectionComponent, DamageModifyEvent>(OnBrainDamageModify);
        SubscribeLocalEvent<BkmDetachedBrainProtectionComponent, IsRottingEvent>(OnBrainIsRotting);
    }

    private void OnDetachedBodyMapInit(Entity<BkmDetachedBodyComponent> ent, ref MapInitEvent args) =>
        _backmenDamageExclusivity.RemoveInjurableIfPresent(ent);

    private void OnDetachedBodyCreated(Entity<BkmDetachedBodyComponent> ent, ref BkmDetachedBodyCreatedEvent args)
    {
        EnsureComp<DamageableComponent>(ent);

        var flammable = EnsureComp<FlammableComponent>(ent);
        flammable.Damage = new DamageSpecifier
        {
            DamageDict = new Dictionary<ProtoId<DamageTypePrototype>, FixedPoint2> { { "Heat", 3 } }
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
}
