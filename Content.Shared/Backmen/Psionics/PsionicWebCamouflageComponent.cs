using Robust.Shared.GameStates;

namespace Content.Shared.Backmen.Psionics;

/// <summary>
/// Marker for spider-web / contact-based psionic camouflage status effect.
/// Stealth visibility is applied to <see cref="StatusEffectComponent.AppliedTo"/>.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class PsionicWebCamouflageComponent : Component
{
    [DataField]
    public float StealthVisibility = 0.33f;
}
