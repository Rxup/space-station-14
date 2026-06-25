using System.Collections.Generic;
using Content.Shared.Body;
using Content.Shared.Body.Part;
using Robust.Shared.Prototypes;

namespace Content.Shared.Backmen.Targeting;

/// <summary>
/// Maps legacy surgery <see cref="BodyPartType"/> values to nubody categories and targeting parts.
/// </summary>
public static class SurgeryBodyPartMapping
{
    public static readonly ProtoId<OrganCategoryPrototype>[] ExternalCategories =
    [
        "Head",
        "Torso",
        "ArmLeft",
        "ArmRight",
        "HandLeft",
        "HandRight",
        "LegLeft",
        "LegRight",
        "FootLeft",
        "FootRight",
        "ArachneAbdomen",
        "ArachneFront",
        "SpiderLegLeft1",
        "SpiderLegLeft2",
        "SpiderLegLeft3",
        "SpiderLegLeft4",
        "SpiderLegRight1",
        "SpiderLegRight2",
        "SpiderLegRight3",
        "SpiderLegRight4",
    ];

    public static readonly ProtoId<OrganCategoryPrototype>[] SpiderLegLeftSlots =
    [
        "SpiderLegLeft1",
        "SpiderLegLeft2",
        "SpiderLegLeft3",
        "SpiderLegLeft4",
    ];

    public static readonly ProtoId<OrganCategoryPrototype>[] SpiderLegRightSlots =
    [
        "SpiderLegRight1",
        "SpiderLegRight2",
        "SpiderLegRight3",
        "SpiderLegRight4",
    ];

    private static readonly HashSet<ProtoId<OrganCategoryPrototype>> LegCategories =
    [
        "LegLeft",
        "LegRight",
        .. SpiderLegLeftSlots,
        .. SpiderLegRightSlots,
    ];

    public static bool IsLegCategory(ProtoId<OrganCategoryPrototype> category) =>
        LegCategories.Contains(category);

    public static bool IsSpiderLegCategory(ProtoId<OrganCategoryPrototype> category) =>
        SpiderLegLeftSlots.Contains(category) || SpiderLegRightSlots.Contains(category);

    public static readonly ProtoId<OrganCategoryPrototype>[] ArachneGraftInstallOrder =
    [
        "ArachneFront",
        "ArachneAbdomen",
        .. SpiderLegLeftSlots,
        .. SpiderLegRightSlots,
    ];

    public static bool IsArachneGraftCategory(ProtoId<OrganCategoryPrototype> category) =>
        category == "ArachneFront"
        || category == "ArachneAbdomen"
        || IsSpiderLegCategory(category);

    /// <summary>
    /// Returns the most recently grafted arachne segment still present on the body.
    /// Removal must proceed in reverse of <see cref="ArachneGraftInstallOrder"/>.
    /// </summary>
    public static bool TryGetLastAttachedArachneGraft(
        EntityUid body,
        BodySystem organBody,
        out ProtoId<OrganCategoryPrototype> category)
    {
        category = default;

        for (var i = ArachneGraftInstallOrder.Length - 1; i >= 0; i--)
        {
            var candidate = ArachneGraftInstallOrder[i];
            if (!organBody.TryGetOrganByCategory(body, candidate, out _))
                continue;

            category = candidate;
            return true;
        }

        return false;
    }

    public static bool CanDetachArachneGraftCategory(
        EntityUid body,
        ProtoId<OrganCategoryPrototype> category,
        BodySystem organBody) =>
        TryGetLastAttachedArachneGraft(body, organBody, out var last) && last == category;

    /// <summary>
    /// Leg count for bodies with grafted arachne organs (four per side).
    /// </summary>
    public const int ArachneRequiredLegCount = 8;

    public static bool IsHumanFootCategory(ProtoId<OrganCategoryPrototype> category) =>
        category == "FootLeft" || category == "FootRight";

    public static bool IsHumanLegOrFootCategory(ProtoId<OrganCategoryPrototype> category) =>
        category == "LegLeft"
        || category == "LegRight"
        || category == "FootLeft"
        || category == "FootRight";

    public static bool TryGetTargetPart(BodyPartType type, BodyPartSymmetry? symmetry, out TargetBodyPart target)
    {
        if (type == BodyPartType.Groin)
            type = BodyPartType.Chest;

        target = (type, symmetry) switch
        {
            (BodyPartType.Head, _) => TargetBodyPart.Head,
            (BodyPartType.Chest, _) => TargetBodyPart.Chest,
            (BodyPartType.Arm, BodyPartSymmetry.Left) => TargetBodyPart.LeftArm,
            (BodyPartType.Arm, BodyPartSymmetry.Right) => TargetBodyPart.RightArm,
            (BodyPartType.Hand, BodyPartSymmetry.Left) => TargetBodyPart.LeftHand,
            (BodyPartType.Hand, BodyPartSymmetry.Right) => TargetBodyPart.RightHand,
            (BodyPartType.Leg, BodyPartSymmetry.Left) => TargetBodyPart.LeftLeg,
            (BodyPartType.Leg, BodyPartSymmetry.Right) => TargetBodyPart.RightLeg,
            (BodyPartType.Foot, BodyPartSymmetry.Left) => TargetBodyPart.LeftFoot,
            (BodyPartType.Foot, BodyPartSymmetry.Right) => TargetBodyPart.RightFoot,
            _ => default,
        };

        return target != default;
    }

    public static bool TryGetCategory(BodyPartType type, BodyPartSymmetry? symmetry, out ProtoId<OrganCategoryPrototype> category)
    {
        if (!TryGetTargetPart(type, symmetry, out var target))
        {
            category = default;
            return false;
        }

        return TargetBodyPartMapping.TryGetCategory(target, out category);
    }

    public static bool TryGetBodyPartType(ProtoId<OrganCategoryPrototype> category, out BodyPartType type, out BodyPartSymmetry? symmetry)
    {
        type = default;
        symmetry = null;

        switch (category.Id)
        {
            case "ArachneAbdomen":
            case "ArachneFront":
                type = BodyPartType.Groin;
                symmetry = BodyPartSymmetry.None;
                return true;
            case "SpiderLegLeft1":
            case "SpiderLegLeft2":
            case "SpiderLegLeft3":
            case "SpiderLegLeft4":
                type = BodyPartType.Leg;
                symmetry = BodyPartSymmetry.Left;
                return true;
            case "SpiderLegRight1":
            case "SpiderLegRight2":
            case "SpiderLegRight3":
            case "SpiderLegRight4":
                type = BodyPartType.Leg;
                symmetry = BodyPartSymmetry.Right;
                return true;
            default:
                break;
        }

        if (!TargetBodyPartMapping.TryGetTargetPart(category, out var target))
            return false;

        return TryGetBodyPartType(target, out type, out symmetry);
    }

    public static bool TryGetBodyPartType(TargetBodyPart target, out BodyPartType type, out BodyPartSymmetry? symmetry)
    {
        target = TargetBodyPartMapping.Normalize(target);

        type = target switch
        {
            TargetBodyPart.Head => BodyPartType.Head,
            TargetBodyPart.Chest => BodyPartType.Chest,
            TargetBodyPart.LeftArm => BodyPartType.Arm,
            TargetBodyPart.RightArm => BodyPartType.Arm,
            TargetBodyPart.LeftHand => BodyPartType.Hand,
            TargetBodyPart.RightHand => BodyPartType.Hand,
            TargetBodyPart.LeftLeg => BodyPartType.Leg,
            TargetBodyPart.RightLeg => BodyPartType.Leg,
            TargetBodyPart.LeftFoot => BodyPartType.Foot,
            TargetBodyPart.RightFoot => BodyPartType.Foot,
            _ => default,
        };

        symmetry = target switch
        {
            TargetBodyPart.LeftArm or TargetBodyPart.LeftHand or TargetBodyPart.LeftLeg or TargetBodyPart.LeftFoot
                => BodyPartSymmetry.Left,
            TargetBodyPart.RightArm or TargetBodyPart.RightHand or TargetBodyPart.RightLeg or TargetBodyPart.RightFoot
                => BodyPartSymmetry.Right,
            _ => BodyPartSymmetry.None,
        };

        return type != default;
    }

    public static bool IsExternalCategory(ProtoId<OrganCategoryPrototype> category) =>
        ExternalCategories.Contains(category);

    private static readonly Dictionary<ProtoId<OrganCategoryPrototype>, ProtoId<OrganCategoryPrototype>> DependentCategories =
        new()
        {
            { "ArmLeft", "HandLeft" },
            { "ArmRight", "HandRight" },
            { "LegLeft", "FootLeft" },
            { "LegRight", "FootRight" },
        };

    /// <summary>
    /// Returns the distal external organ removed with an amputation (hand for arm, foot for leg).
    /// </summary>
    public static bool TryGetDependentCategory(
        ProtoId<OrganCategoryPrototype> category,
        out ProtoId<OrganCategoryPrototype> dependent)
    {
        return DependentCategories.TryGetValue(category, out dependent);
    }

    /// <summary>
    /// Returns the organ category used as the surgery anchor when reattaching a missing external part.
    /// </summary>
    public static bool TryGetReattachAnchorCategory(
        BodyPartType removedPart,
        BodyPartSymmetry? symmetry,
        out ProtoId<OrganCategoryPrototype> anchorCategory)
    {
        anchorCategory = (removedPart, symmetry) switch
        {
            (BodyPartType.Hand, BodyPartSymmetry.Left) => "ArmLeft",
            (BodyPartType.Hand, BodyPartSymmetry.Right) => "ArmRight",
            (BodyPartType.Foot, BodyPartSymmetry.Left) => "LegLeft",
            (BodyPartType.Foot, BodyPartSymmetry.Right) => "LegRight",
            _ => "Torso",
        };

        return true;
    }
}
