using Content.Shared.Backmen.Disease;
using Robust.Shared.Prototypes;

namespace Content.Client.Backmen.Disease;

public sealed class DiseaseSystem : SharedDiseaseSystem
{
    public override void TryAddDisease(EntityUid host, DiseasePrototype addedDisease, DiseaseCarrierComponent? target = null)
    {
        // server-only handling
    }

    public override void TryAddDisease(EntityUid host, ProtoId<DiseasePrototype> addedDisease, DiseaseCarrierComponent? target = null)
    {
        // server-only handling
    }
}
