using Content.Shared.Actions.ActionTypes;

namespace Content.Shared.Backmen.Abilities.Psionics;

[RegisterComponent]
public sealed partial class MetapsionicPowerComponent : Component
{
    [DataField("range"), ViewVariables(VVAccess.ReadWrite)]
    public float Range = 5f;

    public InstantAction? MetapsionicPowerAction = null;
}
