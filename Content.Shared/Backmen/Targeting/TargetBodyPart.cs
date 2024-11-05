namespace Content.Shared.Backmen.Targeting;


/// <summary>
/// Represents and enum of possible target parts.
/// </summary>
/// <remarks>
/// To get all body parts as an Array, use static
/// method in SharedTargetingSystem GetValidParts.
/// </remarks>
[Flags]
public enum TargetBodyPart : byte
{
    Head = 1,
    Torso = 1 << 1,
    LeftArm = 1 << 2,
    RightArm = 1 << 3,
    LeftLeg = 1 << 4,
    RightLeg = 1 << 5,

    Arms = LeftArm | RightArm,
    Legs = LeftLeg | RightLeg,
    All = Head | Torso | LeftArm | RightArm | LeftLeg | RightLeg,
}
