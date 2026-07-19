using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Backmen.Supermatter.Components;
using Content.Shared.Projectiles;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Backmen.Supermatter;

/// <summary>
/// Directional reflectors redirect emitter bolts from the back and sides, but absorb direct front hits.
/// </summary>
[TestFixture]
public sealed class ReflectorTest : GameTest
{
    private static readonly EntProtoId Reflector = "Reflector";
    private static readonly EntProtoId EmitterBolt = "EmitterBolt";

    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [Test]
    public async Task FrontHit_IsNotRedirected()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var entMan = Server.EntMan;
            var physics = entMan.System<SharedPhysicsSystem>();
            var xformSys = entMan.System<SharedTransformSystem>();

            var reflector = entMan.SpawnEntity(Reflector, map.MapCoords);
            xformSys.SetWorldRotation(reflector, Angle.Zero);

            // Hits the output-facing mirror surface.
            var bolt = entMan.SpawnEntity(EmitterBolt, map.MapCoords);
            var boltPhysics = entMan.GetComponent<PhysicsComponent>(bolt);
            physics.SetLinearVelocity(bolt, new Vector2(0f, 10f), body: boltPhysics);

            var projectile = entMan.GetComponent<ProjectileComponent>(bolt);
            var attempt = new ProjectileReflectAttemptEvent(bolt, projectile, false);
            entMan.EventBus.RaiseLocalEvent(reflector, ref attempt);

            Assert.That(attempt.Cancelled, Is.False, "Direct front hits should be absorbed, not redirected.");
        });
    }

    [Test]
    public async Task BackHit_IsRedirectedAlongOutputFace()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var entMan = Server.EntMan;
            var physics = entMan.System<SharedPhysicsSystem>();
            var xformSys = entMan.System<SharedTransformSystem>();

            var reflector = entMan.SpawnEntity(Reflector, map.MapCoords);
            xformSys.SetWorldRotation(reflector, Angle.Zero);

            // Typical emitter-behind setup: bolt travels through the back and should exit forward.
            var bolt = entMan.SpawnEntity(EmitterBolt, map.MapCoords);
            var boltPhysics = entMan.GetComponent<PhysicsComponent>(bolt);
            physics.SetLinearVelocity(bolt, new Vector2(0f, -10f), body: boltPhysics);

            var projectile = entMan.GetComponent<ProjectileComponent>(bolt);
            var attempt = new ProjectileReflectAttemptEvent(bolt, projectile, false);
            entMan.EventBus.RaiseLocalEvent(reflector, ref attempt);

            Assert.That(attempt.Cancelled, Is.True, "Back hits should be redirected.");

            var redirected = physics.GetMapLinearVelocity(bolt, component: boltPhysics);
            var outputDir = Angle.Zero.ToWorldVec();
            Assert.That(Vector2.Dot(Vector2.Normalize(redirected), outputDir), Is.GreaterThan(0.99f));
            Assert.That(redirected.Length(), Is.EqualTo(10f).Within(0.01f));
            Assert.That(
                xformSys.GetWorldRotation(bolt).Degrees,
                Is.EqualTo((outputDir.ToWorldAngle() + projectile.Angle).Degrees).Within(0.1));
            Assert.That(projectile.Shooter, Is.EqualTo(reflector));
        });
    }

    [Test]
    public async Task SideHit_IsRedirectedAlongOutputFace()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var entMan = Server.EntMan;
            var physics = entMan.System<SharedPhysicsSystem>();
            var xformSys = entMan.System<SharedTransformSystem>();

            var reflector = entMan.SpawnEntity(Reflector, map.MapCoords);
            xformSys.SetWorldRotation(reflector, Angle.Zero);

            var bolt = entMan.SpawnEntity(EmitterBolt, map.MapCoords);
            var boltPhysics = entMan.GetComponent<PhysicsComponent>(bolt);
            physics.SetLinearVelocity(bolt, new Vector2(10f, 0f), body: boltPhysics);

            var projectile = entMan.GetComponent<ProjectileComponent>(bolt);
            var attempt = new ProjectileReflectAttemptEvent(bolt, projectile, false);
            entMan.EventBus.RaiseLocalEvent(reflector, ref attempt);

            Assert.That(attempt.Cancelled, Is.True, "Side hits should be redirected.");

            var redirected = physics.GetMapLinearVelocity(bolt, component: boltPhysics);
            var outputDir = Angle.Zero.ToWorldVec();
            Assert.That(Vector2.Dot(Vector2.Normalize(redirected), outputDir), Is.GreaterThan(0.99f));
        });
    }

    [Test]
    public async Task EmitterBehindReflector_IsRedirectedAlongOutputFace()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var entMan = Server.EntMan;
            var physics = entMan.System<SharedPhysicsSystem>();
            var xformSys = entMan.System<SharedTransformSystem>();

            var reflector = entMan.SpawnEntity(Reflector, map.MapCoords);
            // Output faces east toward the containment field.
            xformSys.SetWorldRotation(reflector, Angle.FromDegrees(90));

            var bolt = entMan.SpawnEntity(EmitterBolt, map.MapCoords);
            var boltPhysics = entMan.GetComponent<PhysicsComponent>(bolt);
            physics.SetLinearVelocity(bolt, new Vector2(10f, 0f), body: boltPhysics);

            var projectile = entMan.GetComponent<ProjectileComponent>(bolt);
            var attempt = new ProjectileReflectAttemptEvent(bolt, projectile, false);
            entMan.EventBus.RaiseLocalEvent(reflector, ref attempt);

            Assert.That(attempt.Cancelled, Is.True, "Emitter-behind shots should be redirected forward.");

            var redirected = physics.GetMapLinearVelocity(bolt, component: boltPhysics);
            var outputDir = Angle.FromDegrees(90).ToWorldVec();
            Assert.That(Vector2.Dot(Vector2.Normalize(redirected), outputDir), Is.GreaterThan(0.99f));
        });
    }

    [Test]
    public async Task ReflectorPrototype_HasDirectionalReflectComponent()
    {
        await Server.WaitAssertion(() =>
        {
            var entMan = Server.EntMan;
            var reflector = entMan.SpawnEntity(Reflector, MapCoordinates.Nullspace);

            Assert.That(entMan.HasComponent<DirectionalReflectComponent>(reflector), Is.True);
        });
    }
}
