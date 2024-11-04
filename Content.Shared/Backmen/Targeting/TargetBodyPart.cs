namespace Content.Shared.Backmen.Targeting;

[Flags]
public enum TargetBodyPart : byte
{
    Head = 1,
    Torso = 1 << 1,
    LeftArm = 1 << 2,
    RightArm = 1 << 3,
    LeftLeg = 1 << 4,
    RightLeg = 1 << 5,

    All = Head | Torso | LeftArm | RightArm | LeftLeg | RightLeg,
}
