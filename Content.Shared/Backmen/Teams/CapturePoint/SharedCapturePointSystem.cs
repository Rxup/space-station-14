using Content.Shared.Backmen.Teams.CapturePoint.Components;
using Content.Shared.Examine;
using Content.Shared.Physics;
using Robust.Shared.Physics.Collision.Shapes;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.Shared.Backmen.Teams.CapturePoint;

public abstract class SharedCapturePointSystem : EntitySystem
{
    [Dependency] private readonly FixtureSystem _fixtures = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;

    public const float TicksPerSecond = 1f;

    public override void Initialize()
    {
        base.Initialize();
        UpdatesAfter.Add(typeof(SharedPhysicsSystem));

        SubscribeLocalEvent<BkmCapturePointComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<BkmCapturePointComponent, ExaminedEvent>(OnCptExamine);
    }

    public virtual void UpdateSignals(Entity<BkmCapturePointComponent> ent)
    {

    }

    private void OnCptExamine(Entity<BkmCapturePointComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.CaptureCurrent >= ent.Comp.CaptureMax)
        {
            args.PushText(Loc.GetString("bkm-ctp-captured-"+ent.Comp.Team));
        }
        else
        {
            args.PushText(Loc.GetString("bkm-cpt-capturing",("team",ent.Comp.Team), ("pr", ent.Comp.CaptureCurrent / ent.Comp.CaptureMax * 100f)));
        }
    }

    protected const string CtpFixture = "cpt-fix";

    private void OnMapInit(Entity<BkmCapturePointComponent> ent, ref MapInitEvent args)
    {
        var boundaryPhysics = EnsureComp<PhysicsComponent>(ent);
        var cShape = new PhysShapeCircle(ent.Comp.ZoneRange.Float());
        // Don't need it to be a perfect circle, just need it to be loosely accurate.
        //cShape.CreateLoop(Vector2.Zero, ent.Comp.ZoneRange + 0.25f, true, count: 4);
        _fixtures.TryCreateFixture(
            ent,
            cShape,
            CtpFixture,
            collisionLayer: (int) (CollisionGroup.HighImpassable | CollisionGroup.Impassable |
                                   CollisionGroup.LowImpassable),
            hard: false,
            body: boundaryPhysics);
        _physics.WakeBody(ent, body: boundaryPhysics);

        UpdateSignals(ent);
    }
}
