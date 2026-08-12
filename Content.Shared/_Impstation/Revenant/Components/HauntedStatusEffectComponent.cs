using Robust.Shared.GameStates;

namespace Content.Shared._Impstation.Revenant.Components;

/// <summary>
/// Marker for <see cref="StatusEffectHaunted"/> — applies <see cref="HauntedComponent"/> to the target.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class HauntedStatusEffectComponent : Component;
