
using Content.Shared.Actions;
using Robust.Shared.Prototypes;

namespace Content.Shared.Backmen.Abilities.Psionics;

[RegisterComponent]
public sealed partial class PsionicInvisibilityPowerComponent : Component
{
    [DataField]
    public EntProtoId<InstantActionComponent> ActionPsionicInvisibility = "ActionPsionicInvisibility";
    [DataField]
    public EntProtoId<InstantActionComponent> ActionPsionicInvisibilityOff = "ActionPsionicInvisibilityOff";

    [DataField]
    public int StunSecond = 8;

    public EntityUid? PsionicInvisibilityPowerAction = null;
    public EntityUid? PsionicInvisibilityPowerActionOff = null;
}
