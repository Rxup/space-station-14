
using Robust.Shared.Prototypes;

namespace Content.Shared.Backmen.Abilities.Psionics;

[RegisterComponent]
public sealed partial class PsionicInvisibilityPowerComponent : Component
{
    [DataField]
    public EntProtoId ActionPsionicInvisibility = "ActionPsionicInvisibility";
    [DataField]
    public EntProtoId ActionPsionicInvisibilityOff = "ActionPsionicInvisibilityOff";

    [DataField]
    public int StunSecond = 8;

    public EntityUid? PsionicInvisibilityPowerAction = null;
    public EntityUid? PsionicInvisibilityPowerActionOff = null;
}
