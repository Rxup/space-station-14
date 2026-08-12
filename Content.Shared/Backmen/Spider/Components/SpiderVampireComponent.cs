using Robust.Shared.GameStates;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;
using Robust.Shared.Prototypes;

namespace Content.Shared.Backmen.Spider.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class SpiderVampireComponent : Component
{
    public EntityUid? SpiderVampireEggAction;

    [DataField]
    public float UsingEggTime = 20;

    /// <summary>
    /// Action entity with <c>LimitedCharges</c> — egg budget lives on the action, not here.
    /// </summary>
    [DataField]
    public EntProtoId EggAction = "ActionSpiderVampireEgg";

    [DataField]
    public TimeSpan InitCooldown = TimeSpan.FromMinutes(5);

    [DataField]
    public EntProtoId SpawnEgg = "FoodEggSpiderVampire";

    /// <summary>
    /// How much hunger is consumed when an entity gives birth.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float HungerPerBirth = 75f;
}

[Serializable, NetSerializable]
public sealed partial class SpiderVampireEggDoAfterEvent : SimpleDoAfterEvent;
