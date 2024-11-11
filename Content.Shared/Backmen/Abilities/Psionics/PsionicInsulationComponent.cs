using Robust.Shared.GameStates;

namespace Content.Shared.Backmen.Abilities.Psionics;

[RegisterComponent, NetworkedComponent]
public sealed partial class PsionicInsulationComponent : Component
{
    public bool Passthrough = false;

    public List<String> SuppressedFactions = new();
}
