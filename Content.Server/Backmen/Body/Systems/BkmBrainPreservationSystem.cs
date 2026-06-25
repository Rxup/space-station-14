using Content.Shared.Backmen.Body.OrganRelations;
using Content.Shared.Backmen.Body.Systems;
using Content.Shared.Body;
using Content.Shared.Body.Organ;
using Robust.Server.GameObjects;
using Robust.Shared.Map;

namespace Content.Server.Backmen.Body.Systems;

/// <summary>
/// Keeps brains intact when bodies or organs are burned or dusted by supermatter.
/// </summary>
public sealed class BkmBrainPreservationSystem : EntitySystem
{
    [Dependency] private readonly BkmBodySharedSystem _body = default!;
    [Dependency] private readonly BkmDetachedBodyScatterSystem _scatter = default!;
    [Dependency] private readonly TransformSystem _transform = default!;

    /// <summary>
    /// Ejects and protects a brain organ so it can be used for revival.
    /// </summary>
    public bool TryPreserveBrain(EntityUid organ, EntityCoordinates origin, EntityUid? cause = null)
    {
        if (!HasComp<BrainComponent>(organ) || !TryComp<OrganComponent>(organ, out var organComp))
            return false;

        _body.RemoveOrgan(organ, organComp);
        _scatter.FlingViolentDetached(organ, origin);
        EnsureComp<BkmDetachedBrainProtectionComponent>(organ);

        return true;
    }

    public bool TryPreserveBrain(EntityUid organ, MapCoordinates origin, EntityUid? cause = null) =>
        TryPreserveBrain(organ, _transform.ToCoordinates(origin), cause);

    /// <summary>
    /// Preserves any brains still hosted on external woundables of a mob before full-body burn.
    /// </summary>
    public void PreserveBrainsOnBody(EntityUid body, EntityUid? cause = null)
    {
        if (!TryComp<BodyComponent>(body, out _))
            return;

        var origin = _transform.GetMapCoordinates(body);

        foreach (var woundable in _body.GetWoundableTargets(body))
        {
            foreach (var (organId, _) in _body.GetOrgansForWoundable(woundable))
            {
                if (HasComp<BrainComponent>(organId))
                    TryPreserveBrain(organId, origin, cause);
            }
        }
    }
}
