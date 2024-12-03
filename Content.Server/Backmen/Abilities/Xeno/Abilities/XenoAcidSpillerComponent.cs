using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.Backmen.Abilities.Xeno.Abilities;

[RegisterComponent]
public sealed partial class XenoAcidSpillerComponent : Component
{
    [DataField]
    public string AcidSpitActionId = "ActionXenoSpitMaidAcid";

    [DataField]
    public EntityUid? AcidSpitAction;

    [DataField]
    public EntProtoId BulletSpawnId = "BulletSplashMaidAcid";

    [DataField]
    public SoundSpecifier BulletSound = new SoundPathSpecifier("/Audio/Effects/Fluids/splat.ogg");
}
