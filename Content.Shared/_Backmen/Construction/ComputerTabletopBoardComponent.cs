using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

// ReSharper disable once CheckNamespace
namespace Content.Shared.Construction.Components;

/// <summary>
/// Used for construction graphs in building tabletop computers.
/// </summary>
[RegisterComponent]
public sealed partial class ComputerTabletopBoardComponent : Component
{
    [DataField("prototype", customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string? Prototype { get; private set; }
}
