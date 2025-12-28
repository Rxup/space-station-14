using Content.Shared._Backmen.Economy;

namespace Content.Server._Backmen.Economy;

[RegisterComponent]
public sealed partial class BankMemoryComponent : Component
{
    public string AccountNumber => BankAccount?.Comp.AccountNumber ?? "";
    public string AccountPin => BankAccount?.Comp.AccountPin ?? "";
    public Entity<BankAccountComponent>? BankAccount { get; set; }
}
