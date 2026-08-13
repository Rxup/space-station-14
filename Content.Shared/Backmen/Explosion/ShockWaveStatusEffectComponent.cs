using Robust.Shared.GameStates;

namespace Content.Shared.Backmen.Explosion.Components;

/// <summary>
/// Status effect that enables the local shock-wave distortion overlay.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ShockWaveStatusEffectComponent : Component;
