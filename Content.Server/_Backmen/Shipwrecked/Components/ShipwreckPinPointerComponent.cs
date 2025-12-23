namespace Content.Server._Backmen.Shipwrecked.Components;

[RegisterComponent]
public sealed partial class ShipwreckPinPointerComponent : Component
{
    [ViewVariables(VVAccess.ReadOnly)]
    public ShipwreckedRuleComponent? Rule;
}
