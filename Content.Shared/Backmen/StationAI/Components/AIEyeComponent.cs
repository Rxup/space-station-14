using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Backmen.StationAI;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class AIEyeComponent : Component
{
    public Entity<StationAIComponent>? AiCore;

    public EntProtoId ReturnAction = "AIEyeReturnAction";


    public EntityUid? ReturnActionUid;
    [ViewVariables, AutoNetworkedField]
    public EntityUid? Camera;
    public bool IsProcessingMoveEvent = false;

    public override bool SendOnlyToOwner => true;

    [ViewVariables, AutoNetworkedField]
    public HashSet<(NetEntity, NetCoordinates)> FollowsCameras = new ();

    public EntityUid? CamListUid;
    public EntProtoId CamListAction = "AIEyeCamAction";

    public EntityUid? CamShootUid;
    public EntProtoId CamShootAction = "AIEyeCamShootAction";
}

public sealed partial class AIEyeCampShootActionEvent : WorldTargetActionEvent
{
}

public sealed partial class AIEyeCampActionEvent : InstantActionEvent
{
}

public sealed partial class AIEyePowerActionEvent : InstantActionEvent
{
}

public sealed partial class AIEyePowerReturnActionEvent : InstantActionEvent
{
}

[Serializable, NetSerializable]
public sealed class EyeMoveToCam : BoundUserInterfaceMessage
{
    public NetEntity Uid;
}
