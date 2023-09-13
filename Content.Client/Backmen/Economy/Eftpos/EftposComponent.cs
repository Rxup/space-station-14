using Content.Shared.Backmen.Economy.Eftpos;

namespace Content.Client.Backmen.Economy.Eftpos;

[RegisterComponent]
[Access(typeof(EftposSystem))]
public sealed partial class EftposComponent : SharedEftposComponent
{
}
