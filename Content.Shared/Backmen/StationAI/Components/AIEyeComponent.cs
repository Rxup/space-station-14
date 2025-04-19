using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared.Backmen.StationAI.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class AIEyeComponent : Component
{
    [ViewVariables, AutoNetworkedField]
    public EntityUid? Camera;

    [ViewVariables, AutoNetworkedField]
    public HashSet<(NetEntity, NetCoordinates)> FollowsCameras = new ();

    public override bool SessionSpecific => true;

    public bool IsProcessingMoveEvent = false;
}

public sealed partial class AIEyeCampShootActionEvent : WorldTargetActionEvent
{
}
public sealed partial class AIEyeCampActionEvent : InstantActionEvent
{
}
[Serializable, NetSerializable]
public sealed class EyeMoveToCam : BoundUserInterfaceMessage
{
    public NetEntity Uid;
}
[Serializable, NetSerializable]
public sealed class EyeCamRequest : BoundUserInterfaceMessage
{

}
[Serializable, NetSerializable]
public sealed class EyeCamUpdate : BoundUserInterfaceState
{

}
