using Content.Shared.Backmen.PacifyZone.Components;
using Content.Shared.Humanoid;
using Content.Shared.Interaction.Events;
using Content.Shared.Mindshield.Components;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Physics.Collision.Shapes;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;

namespace Content.Shared.Backmen.PacifyZone;

public sealed class SharedPacifyZoneSystem : EntitySystem
{
    private EntityQuery<MindShieldComponent> _mindShield;
    private EntityQuery<HumanoidAppearanceComponent> _humanoidAppearance;
    private EntityQuery<PacifyZonePacifestedComponent> _pacifyZonePacifested;

    [Dependency] private readonly FixtureSystem _fixtures = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;


    public override void Initialize()
    {
        base.Initialize();

        UpdatesAfter.Add(typeof(SharedPhysicsSystem));

        _mindShield = GetEntityQuery<MindShieldComponent>();
        _humanoidAppearance = GetEntityQuery<HumanoidAppearanceComponent>();
        _pacifyZonePacifested = GetEntityQuery<PacifyZonePacifestedComponent>();

        SubscribeLocalEvent<PacifyZoneComponent, MapInitEvent>(OnStartup);
        SubscribeLocalEvent<PacifyZoneComponent, StartCollideEvent>(OnEntityEnter);
        SubscribeLocalEvent<PacifyZoneComponent, EndCollideEvent>(OnEntityExit);
        SubscribeLocalEvent<PacifyZonePacifestedComponent, AttackAttemptEvent>(OnAttackAttempt);
        SubscribeLocalEvent<PacifyZonePacifestedComponent, ShotAttemptedEvent>(OnShootAttempt);
    }

    private void OnShootAttempt(Entity<PacifyZonePacifestedComponent> ent, ref ShotAttemptedEvent args)
    {
        // Disallow firing guns in all cases.
        _popup.PopupClient(Loc.GetString("pacified-cannot-fire-gun", ("entity", args.Used)), args.User, args.User);
        args.Cancel();
    }

    private void OnAttackAttempt(Entity<PacifyZonePacifestedComponent> ent, ref AttackAttemptEvent args)
    {
        if (args.Target != null && _pacifyZonePacifested.HasComp(args.Target))
            args.Cancel();
    }

    private void OnStartup(Entity<PacifyZoneComponent> ent, ref MapInitEvent args)
    {
        var boundaryPhysics = EnsureComp<PhysicsComponent>(ent);
        var cShape = new PhysShapeCircle(ent.Comp.ZoneRange);
        // Don't need it to be a perfect circle, just need it to be loosely accurate.
        //cShape.CreateLoop(Vector2.Zero, ent.Comp.ZoneRange + 0.25f, true, count: 4);
        _fixtures.TryCreateFixture(
            ent,
            cShape,
            "fix1",
            collisionLayer: (int) (CollisionGroup.HighImpassable | CollisionGroup.Impassable |
                                   CollisionGroup.LowImpassable),
            hard: false,
            body: boundaryPhysics);
        _physics.WakeBody(ent, body: boundaryPhysics);
    }

    private void OnEntityExit(Entity<PacifyZoneComponent> ent, ref EndCollideEvent args)
    {
        var player = args.OtherEntity;
        RemComp<PacifyZoneComponent>(player);
        Log.Debug($"Entity {player:entity} leave PacifyZone {ent.Owner:entity}");
    }

    private void OnEntityEnter(Entity<PacifyZoneComponent> ent, ref StartCollideEvent args)
    {
        var player = args.OtherEntity;

        // Должны выглядеть как люди или иметь плеера за сущностью
        if (!(_humanoidAppearance.HasComp(player) || HasComp<ActorComponent>(player)))
            return;

        // разрешаем с майндшелдом
        if (ent.Comp.ExcludeMindShield && _mindShield.HasComp(player))
            return;

        Log.Debug($"Entity {player:entity} enter PacifyZone {ent.Owner:entity}");
        EnsureComp<PacifyZonePacifestedComponent>(player).ZoneOwner = ent;
    }
}
