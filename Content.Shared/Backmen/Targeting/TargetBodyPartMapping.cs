using Content.Shared.Body;
using Robust.Shared.Prototypes;

namespace Content.Shared.Backmen.Targeting;

/// <summary>
/// Maps between Backmen <see cref="TargetBodyPart"/> and nubody <see cref="OrganCategoryPrototype"/>.
/// </summary>
public static class TargetBodyPartMapping
{
    private static readonly Dictionary<TargetBodyPart, ProtoId<OrganCategoryPrototype>> PartToCategory = new()
    {
        { TargetBodyPart.Head, "Head" },
        { TargetBodyPart.Chest, "Torso" },
        { TargetBodyPart.LeftArm, "ArmLeft" },
        { TargetBodyPart.RightArm, "ArmRight" },
        { TargetBodyPart.LeftHand, "HandLeft" },
        { TargetBodyPart.RightHand, "HandRight" },
        { TargetBodyPart.LeftLeg, "LegLeft" },
        { TargetBodyPart.RightLeg, "LegRight" },
        { TargetBodyPart.LeftFoot, "FootLeft" },
        { TargetBodyPart.RightFoot, "FootRight" },
    };

    private static readonly Dictionary<ProtoId<OrganCategoryPrototype>, TargetBodyPart> CategoryToPart = new()
    {
        { "Head", TargetBodyPart.Head },
        { "Torso", TargetBodyPart.Chest },
        { "ArmLeft", TargetBodyPart.LeftArm },
        { "ArmRight", TargetBodyPart.RightArm },
        { "HandLeft", TargetBodyPart.LeftHand },
        { "HandRight", TargetBodyPart.RightHand },
        { "LegLeft", TargetBodyPart.LeftLeg },
        { "LegRight", TargetBodyPart.RightLeg },
        { "FootLeft", TargetBodyPart.LeftFoot },
        { "FootRight", TargetBodyPart.RightFoot },
    };

    /// <summary>
    /// Legacy Groin flag (1 &lt;&lt; 2) from before groin targeting was removed.
    /// </summary>
    private const TargetBodyPart LegacyGroin = (TargetBodyPart) (1 << 2);

    /// <summary>
    /// Rewrites legacy groin flags to chest for deserialized/networked state.
    /// </summary>
    public static TargetBodyPart Normalize(TargetBodyPart target)
    {
        if (!target.HasFlag(LegacyGroin))
            return target;

        target &= ~LegacyGroin;
        return target | TargetBodyPart.Chest;
    }

    /// <summary>
    /// Returns the nubody organ category for a single (non-composite) target part.
    /// </summary>
    public static bool TryGetCategory(TargetBodyPart part, out ProtoId<OrganCategoryPrototype> category)
    {
        part = Normalize(part);

        if (IsComposite(part) || !PartToCategory.TryGetValue(part, out category))
        {
            category = default;
            return false;
        }

        return true;
    }

    /// <summary>
    /// Returns a target part for an external organ category.
    /// </summary>
    public static bool TryGetTargetPart(ProtoId<OrganCategoryPrototype> category, out TargetBodyPart part)
    {
        return CategoryToPart.TryGetValue(category, out part);
    }

    /// <summary>
    /// Expands composite flags into individual target parts.
    /// </summary>
    public static IEnumerable<TargetBodyPart> EnumerateSingleParts(TargetBodyPart target)
    {
        target = Normalize(target);

        foreach (var part in SharedTargetingSystem.GetValidParts())
        {
            if (target.HasFlag(part))
                yield return part;
        }
    }

    /// <summary>
    /// True when the value combines multiple base parts (e.g. FullArms, All).
    /// </summary>
    public static bool IsComposite(TargetBodyPart part)
    {
        part = Normalize(part);

        var count = 0;
        foreach (var _ in EnumerateSingleParts(part))
        {
            count++;
            if (count > 1)
                return true;
        }

        return false;
    }
}
