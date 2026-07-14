using System.Numerics;
using Content.Shared.Administration.Logs;
using Content.Shared.Backmen.Supermatter.Components;
using Content.Shared.Database;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.Tag;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Reflect;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.Shared.Backmen.Supermatter;

/// <summary>
/// Handles directional reflection of emitter bolts and other reflective projectiles.
/// </summary>
public sealed partial class DirectionalReflectSystem : EntitySystem
{
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private TagSystem _tags = default!;

    public const string EmitterBoltTag = "EmitterBolt";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DirectionalReflectComponent, ProjectileReflectAttemptEvent>(OnProjectileReflectAttempt);
    }

    private void OnProjectileReflectAttempt(
        Entity<DirectionalReflectComponent> ent,
        ref ProjectileReflectAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        if (!TryReflectProjectile(ent, args.ProjUid))
            return;

        args.Cancelled = true;
    }

    private bool TryReflectProjectile(Entity<DirectionalReflectComponent> reflector, EntityUid projectileUid)
    {
        if (!CanReflect(projectileUid, reflector.Comp))
            return false;

        if (!TryComp<PhysicsComponent>(projectileUid, out var physics))
            return false;

        var velocity = _physics.GetMapLinearVelocity(projectileUid, component: physics);
        if (velocity.LengthSquared() < 0.001f)
            return false;

        var outputAngle = _transform.GetWorldRotation(reflector);
        var outputDir = outputAngle.ToWorldVec().Normalized();
        var incomingDir = (-velocity).Normalized();
        var frontAlignment = Vector2.Dot(incomingDir, outputDir);

        // Only shots fired into the output face are absorbed.
        if (frontAlignment > Math.Cos(reflector.Comp.FrontAbsorbAngle.Theta))
            return false;

        // Shots from the back or sides are redirected out through the output face.
        var speed = velocity.Length();
        var targetMapVelocity = outputDir * speed;
        var projectileAngle = TryComp<ProjectileComponent>(projectileUid, out var projectile)
            ? projectile.Angle
            : Angle.Zero;
        var projectileRotation = outputDir.ToWorldAngle() + projectileAngle;

        // Match gun firing so sprite direction and velocity stay aligned.
        var finalLinear = physics.LinearVelocity + targetMapVelocity - velocity;
        _physics.SetLinearVelocity(projectileUid, finalLinear, body: physics);
        _physics.SetAngularVelocity(projectileUid, 0f, body: physics);

        // Push the bolt past the reflector so collision resolution does not skew the trajectory.
        var reflectorPos = _transform.GetWorldPosition(reflector);
        _transform.SetWorldPositionRotation(
            projectileUid,
            reflectorPos + outputDir * 0.55f,
            projectileRotation);

        PlayAudioAndPopup(reflector.Comp, reflector);

        if (projectile != null)
        {
            _adminLogger.Add(
                LogType.BulletHit,
                LogImpact.Medium,
                $"{ToPrettyString(reflector)} directionally reflected {ToPrettyString(projectileUid)} from {ToPrettyString(projectile.Weapon)} shot by {projectile.Shooter}");

            projectile.Shooter = reflector;
            projectile.Weapon = reflector;
            Dirty(projectileUid, projectile);
        }
        else
        {
            _adminLogger.Add(
                LogType.BulletHit,
                LogImpact.Medium,
                $"{ToPrettyString(reflector)} directionally reflected {ToPrettyString(projectileUid)}");
        }

        return true;
    }

    private bool CanReflect(EntityUid projectile, DirectionalReflectComponent comp)
    {
        if (_tags.HasTag(projectile, EmitterBoltTag))
            return (comp.Reflects & ReflectType.Energy) != 0;

        if (!TryComp<ReflectiveComponent>(projectile, out var reflective))
            return false;

        return (comp.Reflects & reflective.Reflective) != 0;
    }

    private void PlayAudioAndPopup(DirectionalReflectComponent comp, EntityUid reflector)
    {
        if (!_net.IsServer)
            return;
    }
}
