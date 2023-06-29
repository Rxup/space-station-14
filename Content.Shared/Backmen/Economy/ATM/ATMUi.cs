using Content.Shared.FixedPoint;
using Robust.Shared.Serialization;

namespace Content.Shared.Backmen.Economy.ATM;

[Serializable, NetSerializable]
public enum ATMUiKey : byte
{
    Key
}
[Serializable, NetSerializable]
public sealed class ATMRequestWithdrawMessage : BoundUserInterfaceMessage
{
    public FixedPoint2 Amount;
    public string AccountPin;
    public ATMRequestWithdrawMessage(FixedPoint2 amount, string accountPin)
    {
        Amount = amount;
        AccountPin = accountPin;
    }
}
