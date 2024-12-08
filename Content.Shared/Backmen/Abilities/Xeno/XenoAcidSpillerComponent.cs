using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared.Backmen.Abilities.Xeno;

[RegisterComponent]
public sealed partial class XenoAcidSpillerComponent : Component
{
    [DataField]
    public EntProtoId AcidSpitActionId = "ActionXenoSpitMaidAcid";

    [DataField]
    public EntityUid? AcidSpitAction;

    [DataField]
    public EntProtoId BulletSpawnId = "BulletSplashMaidAcid";

    [DataField]
    public SoundSpecifier BulletSound = new SoundPathSpecifier("/Audio/Effects/Fluids/splat.ogg");
}
