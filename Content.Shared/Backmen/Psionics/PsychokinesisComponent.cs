using Robust.Shared.Audio;

namespace Content.Shared.Backmen.Abilities.Psionics;

[RegisterComponent]
public sealed partial class PsychokinesisPowerComponent : Component
{
    public EntityUid? PsychokinesisPowerAction = null;

    [DataField("waveSound")]
    public SoundSpecifier WaveSound = new SoundPathSpecifier("/Audio/Nyanotrasen/Mobs/SilverGolem/wave.ogg");

    /// <summary>
    /// Volume control for the spell.
    /// </summary>
    [DataField("waveVolume")]
    public float WaveVolume = 5f;
}
