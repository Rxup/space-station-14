namespace Content.Shared.Backmen.Targeting;

/// <summary>
/// Represents and enum of possible target body parts.
/// </summary>
/// <remarks>
/// To get all body parts as an array, use static
/// method in SharedTargetingSystem GetValidParts.
/// </remarks>
[Flags]
public enum TargetBodyPart : ushort
{
    Head = 1,
    Chest = 1 << 1,
    // 1 << 2 was Groin — removed; nubody uses Torso/Chest only.
    LeftArm = 1 << 3,
    LeftHand = 1 << 4,
    RightArm = 1 << 5,
    RightHand = 1 << 6,
    LeftLeg = 1 << 7,
    LeftFoot = 1 << 8,
    RightLeg = 1 << 9,
    RightFoot = 1 << 10,

    LeftFullArm = LeftArm | LeftHand,
    LeftFullLeg = LeftLeg | LeftFoot,
    RightFullArm = RightArm | RightHand,
    RightFullLeg = RightLeg | RightFoot,

    Hands = LeftHand | RightHand,
    Arms = LeftArm | RightArm,
    Legs = LeftLeg | RightLeg,
    Feet = LeftFoot | RightFoot,

    FullArms = Arms | Hands,
    FullLegs = Feet | Legs,

    BodyMiddle = Chest | FullArms,

    All = Head | Chest | LeftArm | LeftHand | RightArm | RightHand | LeftLeg | LeftFoot | RightLeg | RightFoot,
}
