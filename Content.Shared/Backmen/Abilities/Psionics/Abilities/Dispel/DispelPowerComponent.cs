using Content.Shared.Actions.Events;

namespace Content.Shared.Backmen.Abilities.Psionics;

[RegisterComponent]
public sealed partial class DispelPowerComponent : Component
{
    [DataField("range"), ViewVariables(VVAccess.ReadWrite)]
    public float Range = 10f;

    public EntityUid? DispelPowerAction = null;
}
