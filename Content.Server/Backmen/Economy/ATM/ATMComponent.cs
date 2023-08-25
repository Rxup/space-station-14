using Content.Server.UserInterface;
using Content.Shared.Backmen.Economy.ATM;
using Content.Shared.Store;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Set;

namespace Content.Server.Backmen.Economy.ATM;

[RegisterComponent]
[Access(typeof(ATMSystem))]
[ComponentReference(typeof(SharedAtmComponent))]
public sealed partial class ATMComponent : SharedAtmComponent
{
    [ViewVariables(VVAccess.ReadOnly), DataField("currencyWhitelist", customTypeSerializer: typeof(PrototypeIdHashSetSerializer<CurrencyPrototype>))]
    public HashSet<string> CurrencyWhitelist = new();

    [DataField("soundInsertCurrency")]
    // Taken from: https://github.com/Baystation12/Baystation12 at commit 662c08272acd7be79531550919f56f846726eabb
    public SoundSpecifier SoundInsertCurrency = new SoundPathSpecifier("/Audio/Backmen/Machines/polaroid2.ogg");
    [DataField("soundWithdrawCurrency")]
    // Taken from: https://github.com/Baystation12/Baystation12 at commit 662c08272acd7be79531550919f56f846726eabb
    public SoundSpecifier SoundWithdrawCurrency = new SoundPathSpecifier("/Audio/Backmen/Machines/polaroid1.ogg");
    [DataField("soundApply")]
    // Taken from: https://github.com/Baystation12/Baystation12 at commit 662c08272acd7be79531550919f56f846726eabb
    public SoundSpecifier SoundApply = new SoundPathSpecifier("/Audio/Backmen/Machines/chime.ogg");
    [DataField("soundDeny")]
    // Taken from: https://github.com/Baystation12/Baystation12 at commit 662c08272acd7be79531550919f56f846726eabb
    public SoundSpecifier SoundDeny = new SoundPathSpecifier("/Audio/Backmen/Machines/buzz-sigh.ogg");
}
