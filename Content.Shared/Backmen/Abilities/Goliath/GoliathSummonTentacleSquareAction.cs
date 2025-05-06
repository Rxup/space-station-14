using Content.Shared.Actions;
using Robust.Shared.Prototypes;

namespace Content.Shared.Abilities.Goliath;

public sealed partial class GoliathSummonTentacleSquareAction : EntityWorldTargetActionEvent
{
    [DataField]
    public EntProtoId EntityId = "EffectGoliathTentacleSpawn";

    [DataField]
    public int Range = 1;
}
