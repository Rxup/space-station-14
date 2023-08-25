using Content.Shared.Backmen.Economy.ATM;

namespace Content.Client.Backmen.Economy.ATM;

[RegisterComponent]
[Access(typeof(ATMSystem))]
public sealed partial class ATMComponent : SharedATMComponent
{
    [DataField("offState")]
    public string? OffState;
    [DataField("normalState")]
    public string? NormalState;
}
