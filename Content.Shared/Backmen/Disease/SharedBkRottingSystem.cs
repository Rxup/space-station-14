using Robust.Shared.Prototypes;

namespace Content.Shared.Backmen.Disease;

public abstract class SharedBkRottingSystem : EntitySystem
{
    public static readonly ProtoId<DiseasePoolPrototype> MiasmaPool = "Miasma";
    public static readonly ProtoId<DiseasePoolPrototype> ColdPool = "Cold";
    public static readonly ProtoId<DiseasePoolPrototype> MoldPool = "Mold";

    /// <summary>
    /// Current outbreak disease for a pool without refreshing the timer.
    /// </summary>
    public virtual ProtoId<DiseasePrototype>? GetCurrentPoolDisease(ProtoId<DiseasePoolPrototype> poolId = default)
    {
        // server-only handling
        return null;
    }

    /// <summary>
    /// Current outbreak disease for a pool and refresh that pool's timer.
    /// </summary>
    public virtual ProtoId<DiseasePrototype>? RequestPoolDisease(ProtoId<DiseasePoolPrototype> poolId = default)
    {
        // server-only handling
        return null;
    }
}
