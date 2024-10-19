using Robust.Shared.GameStates;

namespace Content.Shared.Backmen.Shipwrecked.Components;

/// <summary>
/// This is for tracking events.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ShipwreckSurvivorComponent : Component
{
    public override bool SendOnlyToOwner => true;
}
