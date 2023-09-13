using Content.Server.Backmen.Psionics.Glimmer;
using Content.Shared.Audio;
using Content.Shared.Backmen.Psionics.Glimmer;
using Robust.Shared.Audio;

namespace Content.Server.Backmen.Audio;

[RegisterComponent]
[Access(typeof(SharedAmbientSoundSystem), typeof(GlimmerReactiveSystem))]
public sealed partial class GlimmerSoundComponent : Component
{
    [DataField("glimmerTier", required: true), ViewVariables(VVAccess.ReadWrite)] // only for map editing
    public Dictionary<string, SoundSpecifier> Sound { get; set; } = new();

    public bool GetSound(GlimmerTier glimmerTier, out SoundSpecifier? spec)
    {
        return Sound.TryGetValue(glimmerTier.ToString(), out spec);
    }
}
