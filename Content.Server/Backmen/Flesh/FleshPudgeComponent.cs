using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Server.Backmen.Flesh;

[RegisterComponent]
public sealed partial class FleshPudgeComponent : Component
{
    /// <summary>
    /// WorldTargetAction
    /// </summary>
    [DataField("actionThrowWorm", required: true, customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string ActionThrowWorm = "ActionThrowWorm";

    public EntityUid? ThrowWormAction;

    /// <summary>
    /// WorldTargetAction
    /// </summary>
    [DataField("actionAcidSpit", required: true, customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string ActionAcidSpit = "ActionAcidSpit";
    public EntityUid? AcidSpitAction;

    /// <summary>
    /// InstantAction
    /// </summary>
    [DataField("actionAbsorbBloodPool", required: true, customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string ActionAbsorbBloodPool = "ActionAbsorbBloodPool";
    public EntityUid? AbsorbBloodPoolAction;

    [ViewVariables(VVAccess.ReadWrite), DataField("soundThrowWorm")]
    public SoundSpecifier? SoundThrowWorm = new SoundPathSpecifier("/Audio/Animals/Flesh/throw_worm.ogg");

    [ViewVariables(VVAccess.ReadWrite),
     DataField("wormMobSpawnId", customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string WormMobSpawnId = "MobFleshWorm";

    [ViewVariables(VVAccess.ReadWrite),
     DataField("bulletAcidSpawnId", customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string BulletAcidSpawnId = "BulletSplashAcid";

    [DataField("healBloodAbsorbReagents")] public List<ReagentQuantity> HealBloodAbsorbReagents = new()
    {
        new ReagentQuantity("Omnizine", 1, null),
        new ReagentQuantity("DexalinPlus", 0.50, null),
        new ReagentQuantity("Iron", 0.50, null)
    };

    [DataField("bloodAbsorbSound")]
    public SoundSpecifier BloodAbsorbSound = new SoundPathSpecifier("/Audio/Effects/Fluids/splat.ogg");
}
