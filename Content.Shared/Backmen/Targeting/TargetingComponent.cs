using Content.Shared.Backmen.Surgery.Wounds;
using Robust.Shared.GameStates;

namespace Content.Shared.Backmen.Targeting;

/// <summary>
/// Controls entity limb targeting for actions.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true, raiseAfterAutoHandleState: true)]
public sealed partial class TargetingComponent : Component
{
    [ViewVariables, AutoNetworkedField]
    public TargetBodyPart Target = TargetBodyPart.Chest;

    /// <summary>
    /// What is the current integrity of each body part?
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public Dictionary<TargetBodyPart, WoundableSeverity> BodyStatus = new()
    {
        { TargetBodyPart.Head, WoundableSeverity.Healthy },
        { TargetBodyPart.Chest, WoundableSeverity.Healthy },
        { TargetBodyPart.LeftArm, WoundableSeverity.Healthy },
        { TargetBodyPart.LeftHand, WoundableSeverity.Healthy },
        { TargetBodyPart.RightArm, WoundableSeverity.Healthy },
        { TargetBodyPart.RightHand, WoundableSeverity.Healthy },
        { TargetBodyPart.LeftLeg, WoundableSeverity.Healthy },
        { TargetBodyPart.LeftFoot, WoundableSeverity.Healthy },
        { TargetBodyPart.RightLeg, WoundableSeverity.Healthy },
        { TargetBodyPart.RightFoot, WoundableSeverity.Healthy },
    };

    /// <summary>
    /// What noise does the entity play when swapping targets?
    /// </summary>
    [DataField]
    public string SwapSound = "/Audio/Effects/toggleoncombat.ogg";
}
