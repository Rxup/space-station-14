using Robust.Shared.GameStates;

namespace Content.Shared._Backmen.Input;

[RegisterComponent, NetworkedComponent]
[Access(typeof(RMCInputSystem))]
public sealed partial class ActiveInputMoverComponent : Component;
