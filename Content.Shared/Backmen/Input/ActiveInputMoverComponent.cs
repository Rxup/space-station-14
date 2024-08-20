using Robust.Shared.GameStates;

namespace Content.Shared.Backmen.Input;

[RegisterComponent, NetworkedComponent]
[Access(typeof(RMCInputSystem))]
public sealed partial class ActiveInputMoverComponent : Component;
