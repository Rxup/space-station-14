using System.Linq;
using Content.Shared.Movement.Systems;
using Content.Shared.Physics;
using Content.Shared._Impstation.Revenant.Components;
using Content.Shared.Revenant.Components;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Shared.Revenant.EntitySystems;

/// <summary>
/// Makes the revenant solid when the component is applied.
/// Additionally applies a few visual effects.
/// Used for status effect.
/// </summary>
public abstract partial class SharedCorporealSystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private MovementSpeedModifierSystem _movement = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CorporealComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<CorporealComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<CorporealComponent, RefreshMovementSpeedModifiersEvent>(OnRefresh);

        SubscribeLocalEvent<CorporealStatusEffectComponent, StatusEffectAppliedEvent>(OnCorporealStatusApplied);
        SubscribeLocalEvent<CorporealStatusEffectComponent, StatusEffectRemovedEvent>(OnCorporealStatusRemoved);
    }

    private void OnCorporealStatusApplied(Entity<CorporealStatusEffectComponent> ent, ref StatusEffectAppliedEvent args)
    {
        if (_timing.ApplyingState)
            return;

        EnsureComp<CorporealComponent>(args.Target);
    }

    private void OnCorporealStatusRemoved(Entity<CorporealStatusEffectComponent> ent, ref StatusEffectRemovedEvent args)
    {
        RemComp<CorporealComponent>(args.Target);
    }

    private void OnRefresh(EntityUid uid, CorporealComponent component, RefreshMovementSpeedModifiersEvent args)
    {
        args.ModifySpeed(component.MovementSpeedDebuff, component.MovementSpeedDebuff);
    }

    public virtual void OnStartup(EntityUid uid, CorporealComponent component, ComponentStartup args)
    {
        _appearance.SetData(uid, RevenantVisuals.Corporeal, true);

        if (TryComp<FixturesComponent>(uid, out var fixtures) && fixtures.FixtureCount >= 1)
        {
            var fixture = fixtures.Fixtures.First();

            _physics.SetCollisionMask(uid, fixture.Key, fixture.Value, (int) (CollisionGroup.SmallMobMask | CollisionGroup.GhostImpassable), fixtures);
            _physics.SetCollisionLayer(uid, fixture.Key, fixture.Value, (int) CollisionGroup.SmallMobLayer, fixtures);
        }
        _movement.RefreshMovementSpeedModifiers(uid);
    }

    public virtual void OnShutdown(EntityUid uid, CorporealComponent component, ComponentShutdown args)
    {
        _appearance.SetData(uid, RevenantVisuals.Corporeal, false);

        if (TryComp<FixturesComponent>(uid, out var fixtures) && fixtures.FixtureCount >= 1)
        {
            var fixture = fixtures.Fixtures.First();

            _physics.SetCollisionMask(uid, fixture.Key, fixture.Value, (int) CollisionGroup.GhostImpassable, fixtures);
            _physics.SetCollisionLayer(uid, fixture.Key, fixture.Value, 0, fixtures);
        }
        component.MovementSpeedDebuff = 1; //just so we can avoid annoying code elsewhere
        _movement.RefreshMovementSpeedModifiers(uid);
    }
}
