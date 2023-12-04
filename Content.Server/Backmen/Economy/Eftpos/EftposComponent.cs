using Content.Shared.Backmen.Economy;
using Content.Shared.Backmen.Economy.Eftpos;
using Content.Shared.FixedPoint;
using Robust.Shared.Audio;

namespace Content.Server.Backmen.Economy.Eftpos;

[RegisterComponent]
[Access(typeof(EftposSystem), typeof(EconomySystem))]
public sealed partial class EftposComponent : SharedEftposComponent
{
    [ViewVariables] public FixedPoint2? Value { get; set; } = null;
    [ViewVariables] public Entity<BankAccountComponent>? LinkedAccount { get; set; } = null;

    [ViewVariables, DataField("canChangeAccountNumber")]
    public bool CanChangeAccountNumber { get; private set; } = true;
    [ViewVariables] public EntityUid? LockedBy { get; set; } = null;

    [DataField("presetAccountNumber")] public string? PresetAccountNumber { get; private set; } = null;
    [DataField("presetAccountName")] public string? PresetAccountName { get; private set; } = null;

    [DataField("soundApply")]
    // Taken from: https://github.com/Baystation12/Baystation12 at commit 662c08272acd7be79531550919f56f846726eabb
    public SoundSpecifier SoundApply = new SoundPathSpecifier("/Audio/Backmen/Machines/chime.ogg");
    [DataField("soundDeny")]
    // Taken from: https://github.com/Baystation12/Baystation12 at commit 662c08272acd7be79531550919f56f846726eabb
    public SoundSpecifier SoundDeny = new SoundPathSpecifier("/Audio/Backmen/Machines/buzz-sigh.ogg");
}
