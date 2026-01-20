using Robust.Shared.Prototypes;

namespace Content.Shared.Backmen.Disease;

public abstract class SharedDiseaseSystem : EntitySystem
{
    public virtual void TryAddDisease(EntityUid host, DiseasePrototype addedDisease, DiseaseCarrierComponent? target = null)
    {
        // server-only handling
    }

    public virtual void TryAddDisease(EntityUid host,
        ProtoId<DiseasePrototype> addedDisease,
        DiseaseCarrierComponent? target = null)
    {
        // server-only handling
    }

    public virtual void OnPaperRead(EntityUid ent)
    {
        // server-only handling
    }
}
