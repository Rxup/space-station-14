using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared.Backmen.Soul;

[RegisterComponent, NetworkedComponent]
public partial class GolemComponent : Component
{
    // we use these to config stuff via UI before installation
    [ViewVariables(VVAccess.ReadWrite)]
    public string? Master;
    public string? GolemName;
    public EntityUid? PotentialCrystal;

    [DataField("deathSound")]
    public SoundSpecifier DeathSound { get; set; } = new SoundPathSpecifier("/Audio/Effects/Lightning/lightningbolt.ogg");


}
