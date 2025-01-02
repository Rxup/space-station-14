using Content.Shared.Backmen.Surgery.Wounds;
using Robust.Shared.GameStates;

namespace Content.Shared.Backmen.Targeting;

/// <summary>
/// Controls entity limb targeting for actions.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class TargetingComponent : Component
{
    [ViewVariables, AutoNetworkedField]
    public TargetBodyPart Target = TargetBodyPart.Chest;

    /// <summary>
    /// What odds does the entity have of targeting each body part?
    /// </summary>
    [DataField]
    public Dictionary<TargetBodyPart, float> TargetOdds = new()
    {
        { TargetBodyPart.Head, 0.1f },
        { TargetBodyPart.Chest, 0.3f },
        { TargetBodyPart.Groin, 0.1f },
        { TargetBodyPart.LeftArm, 0.1f },
        { TargetBodyPart.LeftHand, 0.05f },
        { TargetBodyPart.RightArm, 0.1f },
        { TargetBodyPart.RightHand, 0.05f },
        { TargetBodyPart.LeftLeg, 0.1f },
        { TargetBodyPart.LeftFoot, 0.05f },
        { TargetBodyPart.RightLeg, 0.1f },
        { TargetBodyPart.RightFoot, 0.05f },
    };

    /// <summary>
    /// What is the current integrity of each body part?
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public Dictionary<TargetBodyPart, WoundableSeverity> BodyStatus = new()
    {
        { TargetBodyPart.Head, WoundableSeverity.Healthy },
        { TargetBodyPart.Chest, WoundableSeverity.Healthy },
        { TargetBodyPart.Groin, WoundableSeverity.Healthy },
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
