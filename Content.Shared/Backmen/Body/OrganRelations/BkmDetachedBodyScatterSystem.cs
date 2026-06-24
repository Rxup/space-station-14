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
public sealed class BkmDetachedBodyScatterSystem : EntitySystem
{
    public const float ViolentScatterMin = 1f;
    public const float ViolentScatterMax = 2.5f;
    public const float ViolentFlingImpulseMultiplier = 2.5f;
    public const float ViolentFlingImpulse = 8f;
    public const float ViolentFlingImpulseVariance = 3f;

    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;

    public void ScatterViolentBundle(
        EntityUid bundle,
        EntityCoordinates origin,
        Vector2? splatDirection = null,
        float splatModifier = 1f)
    {
        var offset = _random.NextFloat(BkmDetachedBodyScatterSystem.ViolentScatterMin,
            BkmDetachedBodyScatterSystem.ViolentScatterMax);
        var world = _transform.ToMapCoordinates(origin).Position + _random.NextAngle().ToVec() * offset;
        _transform.SetWorldPosition(bundle, world);

        _transform.SetLocalRotation(bundle, _random.NextAngle());

        _transform.AttachToGridOrMap(bundle);

        if (!TryComp(bundle, out PhysicsComponent? physics) || physics.BodyType == BodyType.Static)
            return;

        var scatterAngle = splatDirection?.ToAngle() ?? _random.NextAngle();
        var scatterVector = _random.NextAngle(scatterAngle - Angle.FromDegrees(180), scatterAngle + Angle.FromDegrees(180))
            .ToVec() * (ViolentFlingImpulse * splatModifier * ViolentFlingImpulseMultiplier
                        + _random.NextFloat(ViolentFlingImpulseVariance));
        _physics.ApplyLinearImpulse(bundle, scatterVector, body: physics);
    }
}
