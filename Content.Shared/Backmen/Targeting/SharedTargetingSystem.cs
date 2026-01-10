using System.Linq;
using Content.Shared.Humanoid;
using Content.Shared.Localizations;
using Robust.Shared.Utility;
using SharedLoc = Robust.Shared.Localization.Loc;

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
            TargetBodyPart.Chest,
            TargetBodyPart.Groin,
            TargetBodyPart.LeftArm,
            TargetBodyPart.LeftHand,
            TargetBodyPart.LeftLeg,
            TargetBodyPart.LeftFoot,
            TargetBodyPart.RightArm,
            TargetBodyPart.RightHand,
            TargetBodyPart.RightLeg,
            TargetBodyPart.RightFoot,
        };

        return parts;
    }

    public static HumanoidVisualLayers ToVisualLayers(TargetBodyPart targetBodyPart)
    {
        switch (targetBodyPart)
        {
            case TargetBodyPart.Head:
                return HumanoidVisualLayers.Head;
            case TargetBodyPart.Chest:
                return HumanoidVisualLayers.Chest;
            case TargetBodyPart.Groin:
                return HumanoidVisualLayers.Groin;
            case TargetBodyPart.LeftArm:
                return HumanoidVisualLayers.LArm;
            case TargetBodyPart.LeftHand:
                return HumanoidVisualLayers.LHand;
            case TargetBodyPart.RightArm:
                return HumanoidVisualLayers.RArm;
            case TargetBodyPart.RightHand:
                return HumanoidVisualLayers.RHand;
            case TargetBodyPart.LeftLeg:
                return HumanoidVisualLayers.LLeg;
            case TargetBodyPart.LeftFoot:
                return HumanoidVisualLayers.LFoot;
            case TargetBodyPart.RightLeg:
                return HumanoidVisualLayers.RLeg;
            case TargetBodyPart.RightFoot:
                return HumanoidVisualLayers.RFoot;
            default:
                return HumanoidVisualLayers.Chest;
        }
    }

    /// <summary>
    /// Formats a TargetBodyPart enum value into a localized string for guidebook display.
    /// Returns null if the part is All (meaning it affects all body parts).
    /// </summary>
    public static string? FormatTargetBodyPartForGuidebook(TargetBodyPart targetPart)
    {
        if (targetPart == TargetBodyPart.All)
            return null;

        // Check for composite values first (exact matches)
        var compositeName = targetPart switch
        {
            TargetBodyPart.LeftFullArm => "target-body-part-left-full-arm",
            TargetBodyPart.RightFullArm => "target-body-part-right-full-arm",
            TargetBodyPart.LeftFullLeg => "target-body-part-left-full-leg",
            TargetBodyPart.RightFullLeg => "target-body-part-right-full-leg",
            TargetBodyPart.Hands => "target-body-part-hands",
            TargetBodyPart.Arms => "target-body-part-arms",
            TargetBodyPart.Legs => "target-body-part-legs",
            TargetBodyPart.Feet => "target-body-part-feet",
            TargetBodyPart.FullArms => "target-body-part-full-arms",
            TargetBodyPart.FullLegs => "target-body-part-full-legs",
            TargetBodyPart.BodyMiddle => "target-body-part-body-middle",
            TargetBodyPart.FullLegsGroin => "target-body-part-full-legs-groin",
            _ => null
        };

        if (compositeName != null)
            return SharedLoc.GetString(compositeName);

        // Handle individual flags
        var parts = new List<string>();
        var validParts = GetValidParts();

        foreach (var part in validParts)
        {
            if (targetPart.HasFlag(part) && (int)targetPart != (int)(TargetBodyPart.All))
            {
                var partName = part switch
                {
                    TargetBodyPart.Head => "target-body-part-head",
                    TargetBodyPart.Chest => "target-body-part-chest",
                    TargetBodyPart.Groin => "target-body-part-groin",
                    TargetBodyPart.LeftArm => "target-body-part-left-arm",
                    TargetBodyPart.LeftHand => "target-body-part-left-hand",
                    TargetBodyPart.RightArm => "target-body-part-right-arm",
                    TargetBodyPart.RightHand => "target-body-part-right-hand",
                    TargetBodyPart.LeftLeg => "target-body-part-left-leg",
                    TargetBodyPart.LeftFoot => "target-body-part-left-foot",
                    TargetBodyPart.RightLeg => "target-body-part-right-leg",
                    TargetBodyPart.RightFoot => "target-body-part-right-foot",
                    _ => null
                };

                if (partName != null)
                    parts.Add(partName);
            }
        }

        // If we have specific parts, format them
        if (parts.Count > 0)
        {
            var localizedParts = parts.Select(p => SharedLoc.GetString(p)).ToList();
            return ContentLocalizationManager.FormatList(localizedParts);
        }

        // Fallback
        return Enum.GetName(typeof(TargetBodyPart), targetPart) ?? "Unknown";
    }
}
