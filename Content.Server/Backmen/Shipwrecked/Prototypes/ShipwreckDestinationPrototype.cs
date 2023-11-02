using Content.Server.Atmos;
using Content.Shared.Parallax.Biomes;
using Content.Shared.Procedural;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Dictionary;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.List;

namespace Content.Server.Backmen.Shipwrecked.Prototypes;

[Prototype("shipwreckDestination")]
public sealed partial class ShipwreckDestinationPrototype : IPrototype
{
    [ViewVariables]
    [IdDataField]
    public string ID { get; } = default!;

    [ViewVariables]
    [DataField("biome", required: true, customTypeSerializer: typeof(PrototypeIdSerializer<BiomeTemplatePrototype>))]
    public readonly string BiomePrototype = default!;

    [ViewVariables]
    [DataField("lightColor")]
    public readonly Color? LightColor = null; //= MapLightComponent.DefaultColor;

    [ViewVariables]
    [DataField("structureDistance")]
    public readonly int StructureDistance = 80;

    [ViewVariables]
    [DataField("structures", customTypeSerializer: typeof(PrototypeIdDictionarySerializer<int, DungeonConfigPrototype>))]
    public readonly Dictionary<string, int> Structures = new();

    [ViewVariables]
    [DataField("factions", customTypeSerializer: typeof(PrototypeIdListSerializer<ShipwreckFactionPrototype>))]
    public readonly List<string> Factions = new();

    [ViewVariables]
    [DataField("gravity")]
    public readonly bool Gravity = true;

    [ViewVariables]
    [DataField("atmosphere")]
    public readonly GasMixture? Atmosphere = null;
}
