using Robust.Shared.GameStates;

namespace Content.Shared.Backmen.Surgery.Pain.Components;

/// <summary>
/// Prevents pain crit and pain reflexes (falling, screaming, etc.) from affecting this entity.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class PainImmuneComponent : Component;
