using Robust.Shared.GameStates;

namespace Content.Shared.Backmen.PacifyZone.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class PacifyZoneComponent : Component
{
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float ZoneRange = 10f;
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public bool ExcludeMindShield = true;
}
