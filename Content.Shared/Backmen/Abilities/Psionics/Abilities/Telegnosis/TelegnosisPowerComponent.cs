using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared.Backmen.Abilities.Psionics;

[RegisterComponent]
public sealed partial class TelegnosisPowerComponent : Component
{
    [DataField("prototype", customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string Prototype = "MobObserverTelegnostic";
    public EntityUid? TelegnosisPowerAction;
    public EntityUid? TelegnosisProjection;
}
