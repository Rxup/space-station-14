using Robust.Shared.Audio;

namespace Content.Server.DNALocker;

[RegisterComponent]
public sealed partial class DNALockerComponent : Component
{
    [DataField]
    public string? DNA;

    [DataField]
    public bool Locked = false;

    [DataField("lockSound")]
    public SoundSpecifier LockSound = new SoundPathSpecifier("/Audio/_Cats/dna-lock.ogg");
}
