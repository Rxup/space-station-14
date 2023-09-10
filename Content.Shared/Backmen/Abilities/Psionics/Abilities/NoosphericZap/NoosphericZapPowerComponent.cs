using Content.Shared.Actions.ActionTypes;

namespace Content.Shared.Backmen.Abilities.Psionics;

[RegisterComponent]
public sealed partial class NoosphericZapPowerComponent : Component
{
    public EntityTargetAction? NoosphericZapPowerAction = null;
}
