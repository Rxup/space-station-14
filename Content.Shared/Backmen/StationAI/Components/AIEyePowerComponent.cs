using Content.Shared.Actions;
using Robust.Shared.Prototypes;

namespace Content.Shared.Backmen.StationAI;

[RegisterComponent]
public sealed partial class AIEyePowerComponent : Component
{
    [DataField("prototype")]
    public EntProtoId<AIEyeComponent> Prototype = "AIEye";

    [DataField("prototypeAction")]
    public EntProtoId<InstantActionComponent> PrototypeAction = "AIEyeAction";

    public EntityUid? EyePowerAction = null;
}
