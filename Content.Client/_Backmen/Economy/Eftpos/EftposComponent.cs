using Content.Shared._Backmen.Economy.Eftpos;

namespace Content.Client._Backmen.Economy.Eftpos;

[RegisterComponent]
[Access(typeof(EftposSystem))]
public sealed partial class EftposComponent : SharedEftposComponent
{
}
