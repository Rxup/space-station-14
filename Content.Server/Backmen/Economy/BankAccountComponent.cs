using Content.Server.Backmen.CartridgeLoader.Cartridges;
using Content.Server.Backmen.Economy.ATM;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;

namespace Content.Server.Backmen.Economy;

[RegisterComponent, NetworkedComponent]
[Access(typeof(BankManagerSystem), typeof(EconomySystem), typeof(BankCartridgeSystem), typeof(ATMSystem))]
public sealed partial class BankAccountComponent : Component
{
    [ViewVariables(VVAccess.ReadOnly)]
    public string AccountNumber { get; set; } = "000";

    [ViewVariables(VVAccess.ReadOnly)]
    public string AccountPin { get; set; } = "0000";
    [ViewVariables(VVAccess.ReadWrite)]
    public string? AccountName { get; set; }

    [ViewVariables(VVAccess.ReadWrite)]
    public FixedPoint2 Balance { get; set; }

    [ViewVariables(VVAccess.ReadOnly)]
    public string CurrencyType { get; private set; } = "SpaceCash";
    [ViewVariables(VVAccess.ReadOnly)]
    public bool IsInfinite { get; set; }

    [ViewVariables]
    public EntityUid? BankCartridge { get; set; }

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
    /*
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
    */
    public void SetBalance(FixedPoint2 newValue)
    {
        Balance = FixedPoint2.Clamp(newValue, 0, FixedPoint2.MaxValue);
    }
}
