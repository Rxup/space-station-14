using Content.Shared.Body;
using System.Linq;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Humanoid.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared.Backmen.Body.Systems;

public partial class BkmBodySharedSystem
{
    private void InitializePartAppearances()
    {
        // Markings are handled by SharedVisualBodySystem in nubody.
    }

    protected virtual void ApplyPartMarkings(EntityUid target, EntityUid part)
    {
    }

    protected virtual void RemoveBodyMarkings(EntityUid target, EntityUid part, EntityUid body)
    {
    }

    public void ModifyMarkings(
        EntityUid body,
        EntityUid part,
        HumanoidProfileComponent profile,
        HumanoidVisualLayers category,
        string markingId)
    {
        if (!TryComp<VisualOrganMarkingsComponent>(part, out var visualMarkings))
            return;

        if (!visualMarkings.Markings.TryGetValue(category, out var markings))
        {
            markings = new List<Marking>();
            visualMarkings.Markings[category] = markings;
        }

        if (Prototypes.TryIndex<MarkingPrototype>(markingId, out var markingProto))
            markings.Add(markingProto.AsMarking());
        else
            markings.Add(new Marking(markingId, 1));

        Dirty(part, visualMarkings);
    }

    public bool OrganHasMarking(EntityUid part, HumanoidVisualLayers category, string matchString)
    {
        if (!TryComp<VisualOrganMarkingsComponent>(part, out var visualMarkings))
            return false;

        return visualMarkings.Markings.TryGetValue(category, out var layerMarkings)
            && layerMarkings.Any(m => m.MarkingId.Contains(matchString));
    }
}
