using Robust.Shared.GameStates;

namespace Content.Shared._Impstation.Revenant.Components;

/// <summary>
/// Marker on <see cref="StatusEffectStasis"/> entities.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class RevenantStasisStatusEffectComponent : Component;
