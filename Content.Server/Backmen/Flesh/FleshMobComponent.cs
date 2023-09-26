using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Server.Backmen.Flesh;

[RegisterComponent]
public sealed partial class FleshMobComponent : Component
{

    [ViewVariables(VVAccess.ReadWrite), DataField("soundDeath")]
    public SoundSpecifier? SoundDeath = new SoundPathSpecifier("/Audio/Animals/Flesh/flesh_pudge_dead.ogg");

    [ViewVariables(VVAccess.ReadWrite),
     DataField("deathMobSpawnId", customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string DeathMobSpawnId = "MobFleshWorm";

    [DataField("deathMobSpawnCount"), ViewVariables(VVAccess.ReadWrite)]
    public int DeathMobSpawnCount = 0;
}
