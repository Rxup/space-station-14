using Robust.Shared.Prototypes;

namespace Content.Shared.Backmen.Disease;

/// <summary>
/// A rotating disease outbreak pool. Everyone infected from this source
/// gets the same current disease until the pool repicks.
/// </summary>
[Prototype]
public sealed partial class DiseasePoolPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Diseases this pool can rotate through.
    /// </summary>
    [DataField(required: true)]
    public List<ProtoId<DiseasePrototype>> Diseases = new();

    /// <summary>
    /// How long without an infection before the pool picks a new disease.
    /// Infection refreshes this timer.
    /// </summary>
    [DataField]
    public TimeSpan RepickTime = TimeSpan.FromMinutes(5);
}
