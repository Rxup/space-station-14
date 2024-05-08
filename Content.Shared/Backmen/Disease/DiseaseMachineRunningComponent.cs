using Robust.Shared.GameStates;

namespace Content.Shared.Backmen.Disease;

/// <summary>
/// For EntityQuery to keep track of which machines are running
/// </summary>
[RegisterComponent]
[NetworkedComponent]
public sealed partial class DiseaseMachineRunningComponent : Component
{

}
