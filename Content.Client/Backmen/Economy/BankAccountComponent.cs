using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;

namespace Content.Client.Backmen.Economy;

[RegisterComponent, NetworkedComponent]
public sealed partial class BankAccountComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite)]
    public FixedPoint2 Balance { get; set; }
}
