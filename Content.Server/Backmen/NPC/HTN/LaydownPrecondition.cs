using Content.Server.NPC;
using Content.Server.NPC.HTN.Preconditions;
using Content.Shared.Standing;

namespace Content.Server.Backmen.NPC.HTN;

/// <summary>
/// Checks if the owner is laydown or not
/// </summary>
public sealed partial class LaydownPrecondition : HTNPrecondition
{
    private StandingStateSystem _stand = default!;

    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("isDown")]
    public bool IsDown = true;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _stand = sysManager.GetEntitySystem<StandingStateSystem>();
    }

    public override bool IsMet(NPCBlackboard blackboard)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        return IsDown && _stand.IsDown(owner) ||
               !IsDown && !_stand.IsDown(owner);
    }
}
