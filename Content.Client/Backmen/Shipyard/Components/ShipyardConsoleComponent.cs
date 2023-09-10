using Content.Shared.Backmen.Shipyard.Components;

namespace Content.Client.Backmen.Shipyard.Components;

[RegisterComponent]
[ComponentReference(typeof(SharedShipyardConsoleComponent))]
public sealed partial class ShipyardConsoleComponent : SharedShipyardConsoleComponent {}
