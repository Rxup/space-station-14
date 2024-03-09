using Robust.Shared.Audio;

namespace Content.Server.Backmen.Blob;

[RegisterComponent]
public sealed partial class ZombieBlobComponent : Component
{
    public List<string> OldFactions = new();

    public EntityUid BlobPodUid = default!;

    public float? OldColdDamageThreshold = null;

    [ViewVariables]
    public Dictionary<string, int> DisabledFixtureMasks { get; } = new();

    [DataField("greetSoundNotification")]
    public SoundSpecifier GreetSoundNotification = new SoundPathSpecifier("/Audio/Ambience/Antag/zombie_start.ogg");
}
