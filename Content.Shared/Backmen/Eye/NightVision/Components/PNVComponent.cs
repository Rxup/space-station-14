using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared.Backmen.Eye.NightVision.Components;


[RegisterComponent]
[NetworkedComponent]
public sealed partial class PNVComponent : Component
{
    [DataField(customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))] public string ActionProto = "NVToggleAction";
    [DataField] public EntityUid? ActionContainer;
}
