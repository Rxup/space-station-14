using Content.Shared.Backmen.Body.Systems;
using Content.Shared.Body.Events;

namespace Content.Shared.Body;

public sealed partial class BodySystem
{
    [Dependency] private BkmBodySharedSystem _bkmBody = default!;

    partial void RelayApplyMetabolicMultiplierToHierarchy(Entity<BodyComponent> ent, ref ApplyMetabolicMultiplierEvent args)
    {
        foreach (var organ in _bkmBody.GetBodyOrgans(ent, ent))
            RaiseLocalEvent(organ.Id, ref args);
    }
}
