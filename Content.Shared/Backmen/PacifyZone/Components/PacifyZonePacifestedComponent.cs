using Robust.Shared.GameStates;

namespace Content.Shared.Backmen.PacifyZone.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class PacifyZonePacifestedComponent : Component
{
    public EntityUid ZoneOwner = EntityUid.Invalid;
}
