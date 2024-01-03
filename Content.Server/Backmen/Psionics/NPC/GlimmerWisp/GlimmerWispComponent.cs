using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;

namespace Content.Server.Backmen.Psionics.NPC.GlimmerWisp;

[RegisterComponent]
public sealed partial class GlimmerWispComponent : Component
{
    public bool IsDraining = false;
    /// <summary>
    /// The time (in seconds) that it takes to drain an entity.
    /// </summary>
    [DataField("drainDelay")]
    public float DrainDelay = 8.35f;

    [DataField("drainSound")]
    public SoundSpecifier DrainSoundPath = new SoundPathSpecifier("/Audio/Effects/clang2.ogg");

    [DataField("drainFinishSound")]
    public SoundSpecifier DrainFinishSoundPath = new SoundPathSpecifier("/Audio/Effects/guardian_inject.ogg");

    [DataField("drainCancelSound")]
    public SoundSpecifier DrainCancelSoundPath = new SoundPathSpecifier("/Audio/Voice/Human/malescream_guardian.ogg");

    public Entity<AudioComponent>? DrainStingStream;
    public EntityUid? DrainTarget;
}
