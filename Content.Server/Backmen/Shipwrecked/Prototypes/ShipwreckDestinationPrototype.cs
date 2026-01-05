using Content.Server.Atmos;
using Content.Shared.Atmos;
using Content.Shared.Parallax.Biomes;
using Content.Shared.Procedural;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Dictionary;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.List;

namespace Content.Server.Backmen.Shipwrecked.Prototypes;

[Prototype]
public sealed partial class ShipwreckDestinationPrototype : IPrototype
{
    [ViewVariables]
    [IdDataField]
    public string ID { get; private set; } = default!;

    [ViewVariables]
    [DataField("biome", required: true, customTypeSerializer: typeof(PrototypeIdSerializer<BiomeTemplatePrototype>))]
    public string BiomePrototype { get; private set; } = default!;

    [ViewVariables]
    [DataField("lightColor")]
    public Color? LightColor { get; private set; } = null; //= MapLightComponent.DefaultColor;

    [ViewVariables]
    [DataField("structureDistance")]
    public int StructureDistance { get; private set; } = 80;

    [ViewVariables]
    [DataField("structures", customTypeSerializer: typeof(PrototypeIdDictionarySerializer<int, DungeonConfigPrototype>))]
    public Dictionary<string, int> Structures { get; private set; } = new();

    [ViewVariables]
    [DataField("factions", customTypeSerializer: typeof(PrototypeIdListSerializer<ShipwreckFactionPrototype>))]
    public List<string> Factions { get; private set; } = new();

    [ViewVariables]
    [DataField("gravity")]
    public bool Gravity { get; private set; } = true;

    [ViewVariables]
    [DataField("atmosphere")]
    public GasMixture? Atmosphere { get; private set; } = null;

    [DataField("components")]
    [AlwaysPushInheritance]
    public ComponentRegistry Components { get; private set; } = new();
}
