using Robust.Shared.GameStates;

namespace Content.Shared.Backmen.Shipwrecked;

/// <summary>
/// Marks a humanoid that should pose as a corpse until zombified by its proximity trigger.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ZombieSurpriseComponent : Component;
