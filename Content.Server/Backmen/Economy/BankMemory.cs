namespace Content.Server.Backmen.Economy;

[RegisterComponent]
public sealed partial class BankMemoryComponent : Component
{
    public string AccountNumber { get; set; } = "";
    public string AccountPin { get; set; } = "";
    public EntityUid? BankAccount { get; set; }
}
