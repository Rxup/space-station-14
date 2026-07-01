using Content.Server.Shuttles.Components;
using Content.Shared._Mono.ShipGuns;
using Content.Shared.Shuttles.Components;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using System.Linq;
using System.Numerics;

namespace Content.Server._Mono.FireControl;

public sealed partial class FireControlSystem
{
    //Lua start
    /// <summary>
    /// Convenience: attempt to aim and fire all controllable weapons on a grid at a world position.
    /// </summary>
    public void TryAimAndFireGrid(EntityUid gridUid, Vector2 worldTarget)
    {
        if (!TryComp<FireControlGridComponent>(gridUid, out var controlGrid) || controlGrid.ControllingServer == null)
            return;

        if (!TryComp<FireControlServerComponent>(controlGrid.ControllingServer.Value, out var server))
            return;

        // Skip during FTL
        if (server.ConnectedGrid != null && TryComp<FTLComponent>((EntityUid)server.ConnectedGrid, out var ftl) &&
            (ftl.State & (Content.Shared.Shuttles.Systems.FTLState.Starting | Content.Shared.Shuttles.Systems.FTLState.Travelling | Content.Shared.Shuttles.Systems.FTLState.Arriving)) != 0x0)
            return;

        var coords = _xform.ToCoordinates(new MapCoordinates(worldTarget, Transform(gridUid).MapID));

        // Precompute grid forward for nose-arc checks
        var gridRot = _xform.GetWorldRotation(gridUid);
        var gridForward = gridRot.RotateVec(Vector2.UnitY);

        foreach (var weapon in server.Controlled.OrderByDescending(w => GetWeaponPriority(w)))
        {
            if (!TryComp<FireControllableComponent>(weapon, out var comp))
                continue;

            // Check firing arcs before attempting
            if (!TargetWithinArcs(weapon, comp, worldTarget, gridForward))
                continue;

            AttemptFire(weapon, weapon, coords, comp);
        }
    }

    /// <summary>
    /// Convenience: attempt to aim and fire with predictive intercept using a target grid's current velocity.
    /// </summary>
    public void TryAimAndFireGrid(EntityUid gridUid, EntityUid targetGridUid, Vector2 suggestedAim)
    {
        if (!TryComp<FireControlGridComponent>(gridUid, out var controlGrid) || controlGrid.ControllingServer == null)
            return;

        if (!TryComp<FireControlServerComponent>(controlGrid.ControllingServer.Value, out var server))
            return;

        // Skip during FTL
        if (server.ConnectedGrid != null && TryComp<FTLComponent>((EntityUid)server.ConnectedGrid, out var ftl) &&
            (ftl.State & (Content.Shared.Shuttles.Systems.FTLState.Starting | Content.Shared.Shuttles.Systems.FTLState.Travelling | Content.Shared.Shuttles.Systems.FTLState.Arriving)) != 0x0)
            return;

        var mapId = Transform(gridUid).MapID;
        var targetPos = suggestedAim;
        Vector2 targetVel = Vector2.Zero;
        if (TryComp<PhysicsComponent>(targetGridUid, out var targetBody))
        {
            targetVel = targetBody.LinearVelocity;
        }

        var now = _timing.CurTime.TotalSeconds;
        var salvoActive = server.UseSalvos && (now % Math.Max(0.1, server.SalvoPeriodSeconds)) <= server.SalvoWindowSeconds;

        foreach (var weapon in server.Controlled)
        {
            if (!TryComp<FireControllableComponent>(weapon, out var comp))
                continue;

            // Check arcs before attempting
            var gridForward = _xform.GetWorldRotation(gridUid).RotateVec(Vector2.UnitY);
            if (!TargetWithinArcs(weapon, comp, targetPos, gridForward))
                continue;

            // Predict intercept point per-weapon
            Vector2 fireAt = targetPos;
            if (TryComp<GunComponent>(weapon, out var gun))
            {
                var weaponXform = Transform(weapon);
                var weaponPos = _xform.GetWorldPosition(weaponXform);
                var projSpeed = gun.ProjectileSpeedModified > 0f ? gun.ProjectileSpeedModified : 20f;
                var intercept = ComputeIntercept(weaponPos, targetPos, targetVel, projSpeed);
                if (intercept != null)
                    fireAt = intercept.Value;
            }

            // If salvos enabled, fire only inside window, with small jitter per weapon
            if (salvoActive)
            {
                // Determine a deterministic jitter per weapon
                var jitter = (Math.Abs((int)weapon.GetHashCode()) % 1000) / 1000.0 * server.SalvoJitterSeconds;
                if ((now % Math.Max(0.1, server.SalvoPeriodSeconds)) < jitter)
                    continue;
            }

            var coords = _xform.ToCoordinates(new MapCoordinates(fireAt, mapId));
            AttemptFire(weapon, weapon, coords, comp);
        }
    }

    /// <summary>
    /// Returns how many controllable weapons on a grid can currently fire at a world point (arc-only check).
    /// </summary>
    public int CountWeaponsAbleToFireAt(EntityUid gridUid, Vector2 worldTarget)
    {
        if (!TryComp<FireControlGridComponent>(gridUid, out var controlGrid) || controlGrid.ControllingServer == null)
            return 0;
        if (!TryComp<FireControlServerComponent>(controlGrid.ControllingServer.Value, out var server))
            return 0;

        var gridForward = _xform.GetWorldRotation(gridUid).RotateVec(Vector2.UnitY);
        var count = 0;
        foreach (var weapon in server.Controlled.OrderByDescending(w => GetWeaponPriority(w)))
        {
            if (!TryComp<FireControllableComponent>(weapon, out var comp))
                continue;
            if (TargetWithinArcs(weapon, comp, worldTarget, gridForward))
                count++;
        }
        return count;
    }

    private int GetWeaponPriority(EntityUid weapon)
    {
        // Prefer heavy guns when counting potential firepower
        if (TryComp<ShipGunClassComponent>(weapon, out var cls))
        {
            return cls.Class switch
            {
                ShipGunClass.Superheavy => 5,
                ShipGunClass.Heavy => 4,
                ShipGunClass.Medium => 3,
                ShipGunClass.Light => 2,
                ShipGunClass.Superlight => 1,
                _ => 0,
            };
        }
        return 0;
    }

    /// <summary>
    /// Computes an intercept MapCoordinates for a projectile of given speed to hit a target moving with targetVel.
    /// Returns null if no suitable solution.
    /// </summary>
    private Vector2? ComputeIntercept(Vector2 shooterPos, Vector2 targetPos, Vector2 targetVel, float projectileSpeed)
    {
        if (projectileSpeed <= 0f)
            return null;

        var toTarget = targetPos - shooterPos;
        var a = Vector2.Dot(targetVel, targetVel) - projectileSpeed * projectileSpeed;
        var b = 2f * Vector2.Dot(toTarget, targetVel);
        var c = Vector2.Dot(toTarget, toTarget);

        float t;
        const float eps = 1e-4f;
        if (MathF.Abs(a) < eps)
        {
            // Linear solution
            if (MathF.Abs(b) < eps)
                return null;
            t = -c / b;
        }
        else
        {
            var disc = b * b - 4f * a * c;
            if (disc < 0f)
                return null;
            var sqrt = MathF.Sqrt(disc);
            var t1 = (-b + sqrt) / (2f * a);
            var t2 = (-b - sqrt) / (2f * a);
            t = MathF.Min(t1, t2);
            if (t < eps)
                t = MathF.Max(t1, t2);
            if (t < eps)
                return null;
        }

        var intercept = targetPos + targetVel * t;
        return intercept;
    }

    /// <summary>
    /// Returns true if the target is within the weapon's own arc and/or the grid nose arc for AI firing.
    /// Weapon forward is taken as its local +Y (same basis as grid forward).
    /// </summary>
    private bool TargetWithinArcs(EntityUid weapon, FireControllableComponent comp, Vector2 worldTarget, Vector2 gridForward)
    {
        var xform = Transform(weapon);
        var weaponPos = _xform.GetWorldPosition(xform);
        var toTarget = worldTarget - weaponPos;
        if (toTarget.LengthSquared() < 0.0001f)
            return false;
        toTarget = Vector2.Normalize(toTarget);

        // Weapon forward (+Y in local)
        var weaponForward = _xform.GetWorldRotation(xform).RotateVec(Vector2.UnitY);

        // Weapon arc check (treat <=0 or >=360 as always-true)
        var weaponCos = Vector2.Dot(weaponForward, toTarget);
        var weaponAngleOk = comp.FireArcDegrees >= 360f || comp.FireArcDegrees <= 0f ||
                             weaponCos >= MathF.Cos(comp.FireArcDegrees * 0.5f * MathF.PI / 180f);

        if (!comp.UseGridNoseArc)
            return weaponAngleOk;

        // Grid nose arc check (treat <=0 or >=360 as always-true)
        var gridCos = Vector2.Dot(gridForward, toTarget);
        var gridAngleOk = comp.GridNoseArcDegrees >= 360f || comp.GridNoseArcDegrees <= 0f ||
                          gridCos >= MathF.Cos(comp.GridNoseArcDegrees * 0.5f * MathF.PI / 180f);

        // Fire if EITHER arc condition is satisfied
        return weaponAngleOk || gridAngleOk;
    }

    private bool IsInsideAnyFtlExclusion(MapId mapId, Vector2 position)
    {
        var query = EntityQueryEnumerator<FTLExclusionComponent, TransformComponent>();
        while (query.MoveNext(out var excl, out var xform))
        {
            if (!excl.Enabled) continue;
            if (xform.MapID != mapId) continue;
            var center = _xform.GetWorldPosition(xform);
            if ((position - center).Length() <= excl.Range) return true;
        }
        return false;
    }
}
