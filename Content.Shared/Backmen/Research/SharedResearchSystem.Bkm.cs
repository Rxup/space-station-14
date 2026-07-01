using Content.Shared.Research.Components;
using Content.Shared.Research.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared.Research.Systems;

public abstract partial class SharedResearchSystem
{
    // start-backmen: rnd-console-prereqs
    public bool IsTechnologyUnlocked(TechnologyDatabaseComponent component, string technologyId)
    {
        return component.UnlockedTechnologies.Contains(technologyId);
    }

    public bool IsTechnologyUnlocked(TechnologyDatabaseComponent component, ProtoId<TechnologyPrototype> technologyId)
    {
        return IsTechnologyUnlocked(component, technologyId.ToString());
    }
    // end-backmen: rnd-console-prereqs
}
