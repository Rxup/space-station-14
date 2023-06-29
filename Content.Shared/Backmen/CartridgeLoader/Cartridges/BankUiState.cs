using Content.Shared.FixedPoint;
using Robust.Shared.Serialization;

namespace Content.Shared.Backmen.CartridgeLoader.Cartridges;

[Serializable, NetSerializable]
public sealed class BankUiState : BoundUserInterfaceState
{
    public string? LinkedAccountNumber;
    public string? LinkedAccountName;
    public FixedPoint2? LinkedAccountBalance;
    public string? CurrencySymbol;
    public BankUiState(
        string? linkedAccountNumber = null,
        string? linkedAccountName = null,
        FixedPoint2 ? linkedAccountBalance = null,
        string? currencySymbol = null)
    {
        LinkedAccountNumber = linkedAccountNumber;
        LinkedAccountName = linkedAccountName;
        LinkedAccountBalance = linkedAccountBalance;
        CurrencySymbol = currencySymbol;
    }
}
