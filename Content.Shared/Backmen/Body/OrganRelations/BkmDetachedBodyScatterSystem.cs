using System.Numerics;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Random;

namespace Content.Shared.Backmen.Body.OrganRelations;

/// <summary>
/// Shared scatter helpers for detached limb bundles.
/// </summary>
public sealed partial class BkmDetachedBodyScatterSystem : EntitySystem
{
    public const float ViolentScatterMin = 1f;
    public const float ViolentScatterMax = 2.5f;
    public const float ViolentFlingImpulseMultiplier = 2.5f;
    public const float ViolentFlingImpulse = 8f;
    public const float ViolentFlingImpulseVariance = 3f;
    public static readonly Angle ViolentBurstCone = Angle.FromDegrees(360);

    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;

    public void ScatterViolentBundle(
        EntityUid bundle,
        EntityCoordinates origin,
        Vector2? splatDirection = null,
        float splatModifier = 1f)
    {
        FlingViolentDetached(bundle, origin, splatDirection, splatModifier);
    }

    /// <summary>
    /// Launches a detached part from an origin with a physics impulse (no random teleport).
    /// </summary>
    public void FlingViolentDetached(
        EntityUid entity,
        EntityCoordinates origin,
        Vector2? splatDirection = null,
        float splatModifier = 1f,
        Angle? scatterCone = null)
    {
        _transform.SetCoordinates(entity, origin);
        _transform.AttachToGridOrMap(entity);
        _transform.SetLocalRotation(entity, _random.NextAngle());

        if (!TryComp(entity, out PhysicsComponent? physics) || physics.BodyType == BodyType.Static)
            return;

        var cone = scatterCone ?? ViolentBurstCone;
        var scatterAngle = splatDirection is { } dir && dir.LengthSquared() > 0.0001f
            ? dir.ToAngle()
            : _random.NextAngle();
        var scatterVector = _random.NextAngle(scatterAngle - cone / 2, scatterAngle + cone / 2)
            .ToVec() * (ViolentFlingImpulse * splatModifier * ViolentFlingImpulseMultiplier
                        + _random.NextFloat(ViolentFlingImpulseVariance));
        _physics.ApplyLinearImpulse(entity, scatterVector, body: physics);
        _physics.WakeBody(entity, body: physics);
    }
}
