using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared.Backmen.Abilities.Psionics;

[RegisterComponent]
public sealed partial class TelegnosisPowerComponent : Component
{
    [DataField("prototype", customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string Prototype = "MobObserverTelegnostic";
    public EntityUid? TelegnosisPowerAction;
    public Entity<TelegnosticProjectionComponent> TelegnosisProjection;

    /// <summary>
    /// Maximum distance the Telegnosis can travel before it's forced to recall, use YAML to set
    /// </summary>
    [DataField]
    public float DistanceAllowed { get; set; } = 50f;

    [ViewVariables] public ContainerSlot TelegnosisContainer = default!;
}
