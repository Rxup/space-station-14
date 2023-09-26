using Content.Shared.Damage;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Server.Backmen.Flesh;

[Access(typeof(FleshWormSystem))]
[RegisterComponent]
public sealed partial class FleshWormComponent : Component
{
    /// <summary>
    /// WorldTargetAction
    /// </summary>
    [DataField("actionWormJump", required: true, customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string ActionWormJump = "ActionWormJump";

    public EntityUid? WormJumpAction;

    [DataField("paralyzeTime"), ViewVariables(VVAccess.ReadWrite)]
    public float ParalyzeTime = 3f;

    [DataField("chansePounce"), ViewVariables(VVAccess.ReadWrite)]
    public int ChansePounce = 33;

    [DataField("damage", required: true)]
    [ViewVariables(VVAccess.ReadWrite)]
    public DamageSpecifier Damage = default!;

    public bool IsDeath = false;

    public EntityUid EquipedOn;

    [ViewVariables] public float Accumulator = 0;

    [DataField("damageFrequency"), ViewVariables(VVAccess.ReadWrite)]
    public float DamageFrequency = 5;

    [ViewVariables(VVAccess.ReadWrite), DataField("soundWormJump")]
    public SoundSpecifier? SoundWormJump = new SoundPathSpecifier("/Audio/Animals/Flesh/flesh_worm_scream.ogg");

}
