using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Server.Backmen.Flesh;

[RegisterComponent]
public sealed partial class TransformInFleshPudgeOnDeathComponent : Component
{

    [ViewVariables(VVAccess.ReadWrite),
     DataField("fleshPudgeId", customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string FleshPudgeId = "MobFleshPudge";

    [DataField("transformSound")]
    public SoundSpecifier TransformSound = new SoundCollectionSpecifier("gib");

}
