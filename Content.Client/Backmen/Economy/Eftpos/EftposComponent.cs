using Content.Shared.Backmen.Economy.Eftpos;

namespace Content.Client.Backmen.Economy.Eftpos;

[RegisterComponent]
[ComponentReference(typeof(SharedEftposComponent))]
[Access(typeof(EftposSystem))]
public sealed class EftposComponent : SharedEftposComponent
{
}
