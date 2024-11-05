namespace Content.Shared.Backmen.Targeting;

public abstract class SharedTargetingSystem : EntitySystem
{
    /// <summary>
    /// Returns all Valid target body parts as an array.
    /// </summary>
    public static TargetBodyPart[] GetValidParts()
    {
        var parts = new[]
        {
            TargetBodyPart.Head,
            TargetBodyPart.Torso,
            TargetBodyPart.LeftArm,
            TargetBodyPart.LeftLeg,
            TargetBodyPart.RightArm,
            TargetBodyPart.RightLeg,
        };

        return parts;
    }
}
