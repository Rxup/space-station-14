using Content.Shared.FixedPoint;

namespace Content.Server.Backmen.Economy;

public sealed class BankChangeBalanceEvent : HandledEntityEventArgs
{
    public FixedPoint2 OldBalance { get; set; }
    public FixedPoint2 Balance { get; set; }
}
