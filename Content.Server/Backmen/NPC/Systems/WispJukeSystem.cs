using System.Numerics;
using Content.Server.Backmen.Psionics.NPC.GlimmerWisp;
using Content.Server.NPC.Components;
using Content.Server.NPC.Events;
using Content.Server.NPC.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.NPC;
using Robust.Shared.Maths;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Backmen.NPC.Systems;

/// <summary>
/// Flying strafe for glimmer wisps during ranged combat. Tile-based <see cref="NPCJukeSystem"/> is unreliable with gravity ignored.
/// </summary>
public sealed class WispJukeSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GlimmerWispComponent, NPCSteeringEvent>(OnSteering, after: [typeof(NPCJukeSystem)]);
    }

    private void OnSteering(EntityUid uid, GlimmerWispComponent wisp, ref NPCSteeringEvent args)
    {
        if (!HasComp<MovementIgnoreGravityComponent>(uid) || !TryComp<NPCJukeComponent>(uid, out var juke))
            return;

        if (!TryGetThreatDirection(uid, args, out var threatDir))
            return;

        if (_timing.CurTime < juke.NextJuke)
            return;

        var elapsed = _timing.CurTime - juke.NextJuke;

        if (elapsed.TotalSeconds > juke.JukeDuration)
        {
            juke.NextJuke = _timing.CurTime + TimeSpan.FromSeconds(juke.JukeDuration);
            wisp.StrafeSign = _random.Prob(0.5f) ? 1 : -1;
            return;
        }

        Array.Clear(args.Steering.Interest, 0, args.Steering.Interest.Length);

        var strafe = new Vector2(-threatDir.Y, threatDir.X) * wisp.StrafeSign;
        ApplyInterest(args, strafe, 1f);
        args.Steering.CanSeek = false;
    }

    private bool TryGetThreatDirection(EntityUid uid, NPCSteeringEvent args, out Vector2 threatDir)
    {
        threatDir = Vector2.Zero;

        if (TryComp<NPCRangedCombatComponent>(uid, out var gun) &&
            gun.Status != CombatStatus.Unspecified &&
            gun.Target.IsValid() &&
            !Deleted(gun.Target) &&
            gun.Status != CombatStatus.NotInSight &&
            TryComp<TransformComponent>(gun.Target, out var gunTargetXform))
        {
            threatDir = _transform.GetWorldPosition(gunTargetXform) - args.WorldPosition;
            return threatDir.LengthSquared() > 0.01f;
        }

        if (TryComp<NPCSteeringComponent>(uid, out var steering) &&
            steering.Status is SteeringStatus.InRange or SteeringStatus.Moving)
        {
            threatDir = _transform.ToMapCoordinates(steering.Coordinates).Position - args.WorldPosition;
            return threatDir.LengthSquared() > 0.01f;
        }

        return false;
    }

    private static void ApplyInterest(NPCSteeringEvent args, Vector2 direction, float weight)
    {
        direction = args.OffsetRotation.RotateVec(direction);
        var norm = direction.Normalized();

        for (var i = 0; i < SharedNPCSteeringSystem.InterestDirections; i++)
        {
            var result = Vector2.Dot(norm, NPCSteeringSystem.Directions[i]) * weight;

            if (result <= 0f)
                continue;

            args.Steering.Interest[i] = MathF.Max(args.Steering.Interest[i], result);
        }
    }
}
