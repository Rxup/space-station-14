using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager.Attributes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Array;

namespace Content.Shared.Backmen.Targeting;

[Prototype]
public sealed partial class CombatTargetOddsPrototype : IPrototype, IInheritingPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [ParentDataField(typeof(AbstractPrototypeIdArraySerializer<CombatTargetOddsPrototype>))]
    public string[]? Parents { get; private set; }

    [NeverPushInheritance]
    [AbstractDataField]
    public bool Abstract { get; private set; }

    /// <summary>
    /// Aimed part → (actual hit part → weight).
    /// </summary>
    [DataField(required: true)]
    [AlwaysPushInheritance]
    public Dictionary<TargetBodyPart, Dictionary<TargetBodyPart, float>> Spread = new();

    /// <summary>
    /// Optional diagonal-weight multiplier (debug/admin). Primary balance is in YAML spread.
    /// </summary>
    [DataField]
    public float AimedPartWeightMultiplier = 1f;
}
