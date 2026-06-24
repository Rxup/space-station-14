using Content.Shared.Damage;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared.Backmen.Flesh;

[RegisterComponent, NetworkedComponent]
[Access(typeof(SharedFleshWormSystem))]
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

    /// <summary>
    /// Set after a melee hit during HTN combat so the pounce operator can attach to the victim.
    /// </summary>
    [ViewVariables]
    public EntityUid PendingPounceTarget;

    [ViewVariables] public float Accumulator = 0;

    [DataField("damageFrequency"), ViewVariables(VVAccess.ReadWrite)]
    public float DamageFrequency = 5;

    /// <summary>
    /// Chance per head damage tick to inflict organ or bone trauma on the victim's head.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float HeadTraumaChance = 0.25f;

    /// <summary>
    /// Severity passed to the trauma system when <see cref="HeadTraumaChance"/> succeeds.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float TraumaSeverity = 12f;

    [ViewVariables(VVAccess.ReadWrite), DataField("soundWormJump")]
    public SoundSpecifier? SoundWormJump = new SoundPathSpecifier("/Audio/Animals/Flesh/flesh_worm_scream.ogg");

    /// <summary>
    /// How long it takes another person to pull the worm off a victim's face.
    /// </summary>
    [DataField]
    public float RemoveDelay = 3f;

    /// <summary>
    /// Multiplier applied to <see cref="RemoveDelay"/> when the wearer removes the worm themselves.
    /// </summary>
    [DataField]
    public float SelfRemoveDelayMultiplier = 1.5f;

    /// <summary>
    /// Stamina drained from the wearer when they remove the worm themselves.
    /// </summary>
    [DataField]
    public float SelfRemoveStaminaCost = 20f;

    /// <summary>
    /// Status effect applied to victims while this worm covers their face.
    /// </summary>
    [DataField]
    public EntProtoId SuffocationStatus = "StatusEffectFleshWormSuffocation";
}
