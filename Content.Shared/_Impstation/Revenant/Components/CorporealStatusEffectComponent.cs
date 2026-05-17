using Robust.Shared.GameStates;

namespace Content.Shared._Impstation.Revenant.Components;

/// <summary>
/// Marker for <see cref="StatusEffectCorporeal"/> — applies <see cref="CorporealComponent"/> to the target.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CorporealStatusEffectComponent : Component;
