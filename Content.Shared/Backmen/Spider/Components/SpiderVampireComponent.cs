
using Robust.Shared.GameStates;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Robust.Shared.Serialization;
using Robust.Shared.Prototypes;

namespace Content.Shared.Backmen.Spider.Components;


[RegisterComponent, NetworkedComponent]
public sealed partial class SpiderVampireComponent : Component
{
    public EntityUid? SpiderVampireEggAction;
    [DataField]
    public float UsingEggTime = 20;

    [DataField("charges")]
    public int Charges = 1;

    [DataField]
    public TimeSpan InitCooldown = TimeSpan.FromMinutes(5);

    [DataField("spawnEgg")]
    public EntProtoId SpawnEgg = "FoodEggSpiderVampire";

    /// <summary>
    /// How much hunger is consumed when an entity
    /// gives birth. A balancing tool to require feeding.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float HungerPerBirth = 75f;
}

[Serializable, NetSerializable]
public sealed partial class SpiderVampireEggDoAfterEvent : SimpleDoAfterEvent
{

}
