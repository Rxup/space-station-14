namespace Content.Server.Objectives.Components;

/// <summary>
/// Objective condition that requires the player to be a ninja and have doorjacked at least a random number of airlocks.
/// Requires <see cref="NumberObjectiveComponent"/> and <see cref="CounterConditionComponent"/>  to function.
/// </summary>
[RegisterComponent]
public sealed partial class DoorjackConditionComponent : Component;
