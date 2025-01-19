using Robust.Shared.Audio;

namespace Content.Shared.Backmen.Supermatter.Components;

public sealed partial class BkmSupermatterComponent
{
    /// <summary>
    /// Current stream of SM audio.
    /// </summary>
    public EntityUid? AudioStream;

    public SuperMatterSound? SmSound;

    [DataField("dustSound")]
    public SoundSpecifier DustSound = new SoundPathSpecifier("/Audio/Backmen/Supermatter/dust.ogg");

    [DataField("delamSound")]
    public SoundSpecifier DelamSound = new SoundPathSpecifier("/Audio/Backmen/Supermatter/delamming.ogg");

    [DataField]
    public SoundSpecifier CalmSound = new SoundPathSpecifier("/Audio/Backmen/Supermatter/calm.ogg");

    [DataField]
    public SoundSpecifier CurrentSoundLoop = new SoundPathSpecifier("/Audio/Backmen/Supermatter/calm.ogg");

    [DataField("delamAlarm")]
    public SoundSpecifier DelamAlarm = new SoundPathSpecifier("/Audio/Machines/alarm.ogg");
}
