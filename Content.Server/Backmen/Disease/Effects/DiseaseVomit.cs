using Content.Server.Medical;
using Content.Shared.Backmen.Disease;
using JetBrains.Annotations;

namespace Content.Server.Backmen.Disease.Effects;

/// <summary>
/// Forces you to vomit.
/// </summary>
[UsedImplicitly]
public sealed partial class DiseaseVomit : DiseaseEffect
{
    /// How many units of thirst to add each time we vomit
    [DataField("thirstAmount")]
    public float ThirstAmount = -40f;
    /// How many units of hunger to add each time we vomit
    [DataField("hungerAmount")]
    public float HungerAmount = -40f;
}
public sealed partial class DiseaseEffectSystem
{
    [Dependency] private readonly VomitSystem _vomit = default!;
    private void DiseaseVomit(DiseaseEffectArgs args, DiseaseVomit ds)
    {
        _vomit.Vomit(args.DiseasedEntity, ds.ThirstAmount, ds.HungerAmount);
    }
}
