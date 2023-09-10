using Content.Server.Backmen.Economy;
using Content.Shared.FixedPoint;

namespace Content.Server.Backmen.CartridgeLoader.Cartridges;

[RegisterComponent]
public sealed partial class BankCartridgeComponent : Component
{
    [Dependency] private readonly IEntityManager _entityManager = default!;

    [ViewVariables] public BankAccountComponent? LinkedBankAccount { get; set; }

    [ViewVariables] public string? BankAccountNumber;
    [ViewVariables] public string? BankAccountPin;
    [ViewVariables] public string? BankAccountName;
    [ViewVariables] public FixedPoint2? BankAccountBalance;

    public void OnChangeBankAccountBalance(FixedPoint2 amount)
    {
        BankAccountBalance = LinkedBankAccount?.Balance;
        var newEvent = new ChangeBankAccountBalanceEvent(amount, BankAccountBalance);
        _entityManager.EventBus.RaiseLocalEvent(Owner, newEvent);
    }
}
public sealed class ChangeBankAccountBalanceEvent : EntityEventArgs
{
    public readonly FixedPoint2? ChangeAmount;
    public readonly FixedPoint2? NewBalance;
    public ChangeBankAccountBalanceEvent(FixedPoint2? changeAmount, FixedPoint2? newBalance)
    {
        ChangeAmount = changeAmount;
        NewBalance = newBalance;
    }
}
