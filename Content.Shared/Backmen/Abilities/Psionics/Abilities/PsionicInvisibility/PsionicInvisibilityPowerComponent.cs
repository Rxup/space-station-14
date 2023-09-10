using Content.Shared.Actions.ActionTypes;

namespace Content.Shared.Backmen.Abilities.Psionics;

[RegisterComponent]
public sealed partial class PsionicInvisibilityPowerComponent : Component
{
    public InstantAction? PsionicInvisibilityPowerAction = null;
}
