using Content.Shared.FixedPoint;

namespace Content.Server.Backmen.Economy;

[RegisterComponent]
public sealed class BankAccountComponent : Component
{
    public event Action<FixedPoint2>? OnChangeValue;

    [ViewVariables(VVAccess.ReadOnly)]
    public string AccountNumber { get; } = "000";

    [ViewVariables(VVAccess.ReadOnly)]
    public string AccountPin { get; } = "0000";
    [ViewVariables(VVAccess.ReadWrite)]
    public string? AccountName { get; set; }

    [ViewVariables(VVAccess.ReadWrite)]
    public FixedPoint2 Balance { get; set; }

    [ViewVariables(VVAccess.ReadOnly)]
    public string CurrencyType { get; } = "SpaceCash";
    [ViewVariables(VVAccess.ReadOnly)]
    public bool IsInfinite { get; }

    public BankAccountComponent()
    {

    }
    public BankAccountComponent(string accountNumber, string accountPin, string currencyType = "SpaceCash", string? accountName = null, bool isInfinite = false)
    {
        AccountNumber = accountNumber;
        AccountPin = accountPin;
        AccountName = accountName;
        Balance = 0;
        CurrencyType = currencyType;
        IsInfinite = isInfinite;
    }
    public bool TryChangeBalanceBy(FixedPoint2 amount)
    {
        if (IsInfinite)
            return true;
        if (Balance + amount < 0)
            return false;
        var oldBalance = Balance;
        SetBalance(Balance + amount);
        OnChangeValue?.Invoke(amount);
        return true;
    }
    public void SetBalance(FixedPoint2 newValue)
    {
        Balance = FixedPoint2.Clamp(newValue, 0, FixedPoint2.MaxValue);
    }
}
