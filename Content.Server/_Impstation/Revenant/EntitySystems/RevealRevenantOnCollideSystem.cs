using Content.Shared.Physics;
using Content.Shared._Impstation.Revenant.Components;
using Content.Shared._Impstation.Revenant.EntitySystems;
using Robust.Shared.Physics.Collision.Shapes;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.Server._Impstation.Revenant.EntitySystems;

public sealed partial class RevealRevenantOnCollideSystem : SharedRevealRevenantOnCollideSystem
{
    [Dependency] private FixtureSystem _fixtures = default!;
    [Dependency] private CollisionWakeSystem _collisionWake = default!;

    private const string FixtureId = "revenantReveal";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RevealRevenantOnCollideComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<RevealRevenantOnCollideComponent, ComponentShutdown>(OnShutdown);
    }

    private IPhysShape GetOrCreateShape(EntityUid uid)
    {
        if (TryComp(uid, out FixturesComponent? fixtures)
            && fixtures.Fixtures.TryGetValue("fix1", out var fix))
            return fix.Shape;

        return new PhysShapeCircle(0.35f);
    }

    private void OnMapInit(EntityUid uid, RevealRevenantOnCollideComponent comp, MapInitEvent args)
    {
        FixturesComponent? manager = null;
        EnsureComp<PhysicsComponent>(uid);
        _fixtures.TryCreateFixture(uid,
            GetOrCreateShape(uid),
            FixtureId,
            hard: false,
            collisionMask: (int) CollisionGroup.GhostImpassable,
            collisionLayer: (int) CollisionGroup.GhostImpassable,
            manager: manager);

        var collisionWake = EnsureComp<CollisionWakeComponent>(uid);
        _collisionWake.SetEnabled(uid, false, collisionWake);
    }

    private void OnShutdown(EntityUid uid, RevealRevenantOnCollideComponent comp, ComponentShutdown args)
    {
        _fixtures.DestroyFixture(uid, FixtureId);
    }
}
