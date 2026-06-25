using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared.Backmen.Body.Systems;
using Content.Shared.Body;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Part;
using Content.Shared.Humanoid;
using Content.Shared.Localizations;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using SharedLoc = Robust.Shared.Localization.Loc;

namespace Content.Shared.Backmen.Targeting;

public abstract partial class SharedTargetingSystem : EntitySystem
{
    [Dependency] private BodySystem _body = default!;

    private EntityQuery<BodyComponent> _bodyQuery;
    private EntityQuery<OrganComponent> _organQuery;

    public override void Initialize()
    {
        base.Initialize();

        _bodyQuery = GetEntityQuery<BodyComponent>();
        _organQuery = GetEntityQuery<OrganComponent>();
    }

    /// <summary>
    /// Resolves a target part to a nubody external organ entity.
    /// For composite flags, returns the first matching part.
    /// </summary>
    public bool TryGetTargetEntity(EntityUid body, TargetBodyPart target, [NotNullWhen(true)] out EntityUid entity)
    {
        foreach (var candidate in GetTargetEntities(body, target))
        {
            entity = candidate;
            return true;
        }

        entity = default;
        return false;
    }

    /// <summary>
    /// Resolves all entities matching a target part (expands composite flags).
    /// </summary>
    public IEnumerable<EntityUid> GetTargetEntities(EntityUid body, TargetBodyPart target)
    {
        foreach (var single in TargetBodyPartMapping.EnumerateSingleParts(target))
        {
            if (!TargetBodyPartMapping.TryGetCategory(single, out var category))
                continue;

            if (_body.TryGetOrganByCategory(body, category, out var organ))
                yield return organ;
        }
    }

    /// <summary>
    /// Resolves a single (non-composite) target part to a nubody organ entity.
    /// </summary>
    public bool TryGetOrganForTarget(
        EntityUid body,
        TargetBodyPart target,
        [NotNullWhen(true)] out Entity<OrganComponent> organ)
    {
        organ = default;

        if (!TargetBodyPartMapping.TryGetCategory(target, out var category))
            return false;

        return _body.TryGetOrganByCategory(body, category, out organ);
    }

    /// <summary>
    /// Maps a nubody organ back to a target part.
    /// </summary>
    public TargetBodyPart? GetTargetBodyPart(EntityUid entity)
    {
        if (_organQuery.TryComp(entity, out var organ) && organ.Category is { } category
            && TargetBodyPartMapping.TryGetTargetPart(category, out var targetPart))
            return targetPart;

        return null;
    }

    /// <summary>
    /// Returns entities that can be selected as surgery targets on this body.
    /// </summary>
    public IEnumerable<EntityUid> GetSurgeryTargets(EntityUid body)
    {
        foreach (var category in SurgeryBodyPartMapping.ExternalCategories)
        {
            if (_body.TryGetOrganByCategory(body, category, out var organ))
                yield return organ;
        }
    }

    /// <summary>
    /// Resolves a surgery part type to an external organ entity.
    /// </summary>
    public bool TryGetEntityByBodyPartType(
        EntityUid body,
        BodyPartType type,
        BodyPartSymmetry? symmetry,
        [NotNullWhen(true)] out EntityUid entity)
    {
        if (type == BodyPartType.Groin)
            type = BodyPartType.Chest;

        if (SurgeryBodyPartMapping.TryGetCategory(type, symmetry, out var category)
            && _body.TryGetOrganByCategory(body, category, out var organ))
        {
            entity = organ;
            return true;
        }

        if (!TryComp<BodyComponent>(body, out var bodyComp))
        {
            entity = default;
            return false;
        }

        foreach (var contained in bodyComp.Organs?.ContainedEntities ?? [])
        {
            if (TerminatingOrDeleted(contained)
                || !TryComp<OrganComponent>(contained, out var organComp)
                || organComp.Category is not { } organCategory
                || !SurgeryBodyPartMapping.TryGetBodyPartType(organCategory, out var organType, out var organSymmetry))
                continue;

            if (organType != type)
                continue;

            if (symmetry != null && organSymmetry != symmetry)
                continue;

            entity = contained;
            return true;
        }

        entity = default;
        return false;
    }

    /// <summary>
    /// Checks whether an entity matches the given legacy surgery part type.
    /// </summary>
    public bool MatchesBodyPartType(EntityUid entity, BodyPartType type, BodyPartSymmetry? symmetry)
    {
        if (type == BodyPartType.Groin)
            type = BodyPartType.Chest;

        if (_organQuery.TryComp(entity, out var organ)
            && organ.Category is { } category
            && SurgeryBodyPartMapping.TryGetBodyPartType(category, out var organType, out var organSym))
        {
            var symMatch = symmetry == null || organSym == symmetry;
            return organType == type && symMatch;
        }

        return false;
    }

    /// <summary>
    /// Returns the legacy surgery part type for a surgery target entity, if any.
    /// </summary>
    public BodyPartType? GetBodyPartType(EntityUid entity)
    {
        if (_organQuery.TryComp(entity, out var organ)
            && organ.Category is { } category
            && SurgeryBodyPartMapping.TryGetBodyPartType(category, out var type, out _))
            return type;

        return null;
    }

    public BodyPartSymmetry? GetBodyPartSymmetry(EntityUid entity)
    {
        if (_organQuery.TryComp(entity, out var organ)
            && organ.Category is { } category
            && SurgeryBodyPartMapping.TryGetBodyPartType(category, out _, out var symmetry))
            return symmetry;

        return null;
    }

    /// <summary>
    /// Returns organ categories corresponding to the given target part(s).
    /// </summary>
    public IEnumerable<ProtoId<OrganCategoryPrototype>> GetCategoriesForTarget(TargetBodyPart target)
    {
        var seen = new HashSet<ProtoId<OrganCategoryPrototype>>();

        foreach (var single in TargetBodyPartMapping.EnumerateSingleParts(target))
        {
            if (!TargetBodyPartMapping.TryGetCategory(single, out var category) || !seen.Add(category))
                continue;

            yield return category;
        }
    }

    /// <summary>
    /// Normalizes legacy groin targeting values to chest.
    /// </summary>
    public static TargetBodyPart NormalizeTarget(TargetBodyPart target) => TargetBodyPartMapping.Normalize(target);

    /// <summary>
    /// Returns all valid target body parts as an array.
    /// </summary>
    public static TargetBodyPart[] GetValidParts()
    {
        var parts = new[]
        {
            TargetBodyPart.Head,
            TargetBodyPart.Chest,
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
        targetBodyPart = NormalizeTarget(targetBodyPart);

        switch (targetBodyPart)
        {
            case TargetBodyPart.Head:
                return HumanoidVisualLayers.Head;
            case TargetBodyPart.Chest:
                return HumanoidVisualLayers.Chest;
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
    /// Formats a legacy surgery part type into a localized display name.
    /// </summary>
    public static string FormatBodyPartType(BodyPartType type, BodyPartSymmetry? symmetry)
    {
        if (!SurgeryBodyPartMapping.TryGetTargetPart(type, symmetry, out var target))
            return type.ToString();

        return FormatTargetBodyPartForGuidebook(target) ?? type.ToString();
    }

    /// <summary>
    /// Formats a TargetBodyPart enum value into a localized string for guidebook display.
    /// Returns null if the part is All (meaning it affects all body parts).
    /// </summary>
    public static string? FormatTargetBodyPartForGuidebook(TargetBodyPart targetPart)
    {
        targetPart = NormalizeTarget(targetPart);

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
