using Content.Shared.Backmen.Soul;
using Robust.Shared.Audio;

namespace Content.Server.Backmen.Soul;

[RegisterComponent]
public sealed partial class GolemComponent : SharedGolemComponent
{
    // we use these to config stuff via UI before installation
    public string? Master;
    public string? GolemName;
    public EntityUid? PotentialCrystal;

    [DataField("deathSound")]
    public SoundSpecifier DeathSound { get; set; } = new SoundPathSpecifier("/Audio/Effects/Lightning/lightningbolt.ogg");
}
