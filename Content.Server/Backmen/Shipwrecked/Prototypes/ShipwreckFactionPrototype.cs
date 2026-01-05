using Content.Shared.Storage;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Server.Backmen.Shipwrecked.Prototypes;

[Prototype]
public sealed partial class ShipwreckFactionPrototype : IPrototype
{
    [ViewVariables]
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// This entity is always spawned near an objective.
    /// </summary>
    [ViewVariables]
    [DataField("objectiveDefender", customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string? ObjectiveDefender { get; private set; }

    /// <summary>
    /// These entities are placed more carefully.
    /// </summary>
    [ViewVariables]
    [DataField("active")]
    public List<EntitySpawnEntry> Active { get; private set; } = new();

    /// <summary>
    /// These entities are strewn around wherever.
    /// </summary>
    [ViewVariables]
    [DataField("inactive")]
    public List<EntitySpawnEntry> Inactive { get; private set; } = new();
}
