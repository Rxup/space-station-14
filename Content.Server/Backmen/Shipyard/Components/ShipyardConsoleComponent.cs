using Content.Shared.Backmen.Shipyard.Components;

namespace Content.Server.Backmen.Shipyard.Components;

[RegisterComponent]
[ComponentReference(typeof(SharedShipyardConsoleComponent))]
public sealed partial class ShipyardConsoleComponent : SharedShipyardConsoleComponent {}
