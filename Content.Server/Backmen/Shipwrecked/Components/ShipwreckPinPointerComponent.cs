namespace Content.Server.Backmen.Shipwrecked.Components;

[RegisterComponent]
public sealed partial class ShipwreckPinPointerComponent : Component
{
    [ViewVariables(VVAccess.ReadOnly)]
    public ShipwreckedRuleComponent? Rule;
}
