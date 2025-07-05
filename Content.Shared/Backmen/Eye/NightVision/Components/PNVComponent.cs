using Content.Shared.Actions.Components;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Backmen.Eye.NightVision.Components;


[RegisterComponent, NetworkedComponent]
public sealed partial class PNVComponent : Component
{
    [DataField] public EntProtoId<InstantActionComponent> ActionProto = "NVToggleAction";
    [DataField] public EntityUid? ActionContainer;
}
