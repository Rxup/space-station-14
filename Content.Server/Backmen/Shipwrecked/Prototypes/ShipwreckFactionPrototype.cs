using Content.Shared.Storage;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Server.Backmen.Shipwrecked.Prototypes;

[Prototype("shipwreckFaction")]
public sealed partial class ShipwreckFactionPrototype : IPrototype
{
    [ViewVariables]
    [IdDataField]
    public string ID { get; } = default!;

    /// <summary>
    /// This entity is always spawned near an objective.
    /// </summary>
    [ViewVariables]
    [DataField("objectiveDefender", customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public readonly string? ObjectiveDefender;

    /// <summary>
    /// These entities are placed more carefully.
    /// </summary>
    [ViewVariables]
    [DataField("active")]
    public readonly List<EntitySpawnEntry> Active = new();

    /// <summary>
    /// These entities are strewn around wherever.
    /// </summary>
    [ViewVariables]
    [DataField("inactive")]
    public readonly List<EntitySpawnEntry> Inactive = new();
}
