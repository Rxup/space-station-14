using Robust.Shared.GameStates;

namespace Content.Shared._Backmen.Shipwrecked.Components;

/// <summary>
/// This is for tracking events.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ShipwreckSurvivorComponent : Component
{
    public override bool SendOnlyToOwner => true;
}
