using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared.Backmen.Arachne;
using Content.Shared.Backmen.Targeting;
using Content.Shared.Body;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Part;
using Content.Shared.Gibbing;
using Content.Shared.Gibbing.Components;
using Content.Shared.Gibbing.Events;
using Robust.Shared.Prototypes;
namespace Content.Shared.Backmen.Body.Systems;

public partial class BkmBodySharedSystem
{
    private static readonly Dictionary<ProtoId<OrganCategoryPrototype>, ProtoId<OrganCategoryPrototype>> InternalOrganHostCategory =
      new()
      {
          { "Brain", "Head" },
          { "Eyes", "Head" },
          { "Ears", "Head" },
          { "Tongue", "Head" },
          { "Heart", "Torso" },
          { "Lungs", "Torso" },
          { "Stomach", "Torso" },
          { "Liver", "Torso" },
          { "Kidneys", "Torso" },
          { "Appendix", "Torso" },
      };

    /// <summary>
    /// Returns woundable surgery targets: nubody external organs.
    /// </summary>
    public IEnumerable<EntityUid> GetWoundableTargets(EntityUid bodyId, BodyComponent? body = null)
    {
        if (!Resolve(bodyId, ref body, logMissing: false) || body!.Organs == null)
            yield break;

        foreach (var organUid in body.Organs.ContainedEntities.ToArray())
        {
            if (TerminatingOrDeleted(organUid))
                continue;

            if (!TryComp<OrganComponent>(organUid, out var organ) || organ.Category is not { } category)
                continue;

            if (SurgeryBodyPartMapping.IsExternalCategory(category))
                yield return organUid;
        }
    }

    /// <summary>
    /// Resolves a woundable target by surgery part type.
    /// </summary>
    public bool TryGetWoundableTargetByType(
        EntityUid bodyId,
        BodyPartType type,
        BodyPartSymmetry? symmetry,
        [NotNullWhen(true)] out EntityUid target,
        BodyComponent? body = null)
    {
        if (type == BodyPartType.Groin)
            type = BodyPartType.Chest;

        if (!SurgeryBodyPartMapping.TryGetCategory(type, symmetry, out var category)
            || !_nubody.TryGetOrganByCategory((bodyId, body), category, out var organ))
        {
            target = default;
            return false;
        }

        target = organ;
        return true;
    }

    /// <summary>
    /// Whether this body template ever included the given external part (from <see cref="InitialBodyComponent"/>).
    /// Filters animal aggregate attach surgeries (Legs/Hands/Feet) from humanoid per-side slots.
    /// </summary>
    public bool BodyExpectsReattachPart(EntityUid bodyId, BodyPartType part, BodyPartSymmetry? symmetry)
    {
        if (!TryComp<InitialBodyComponent>(bodyId, out var initialBody))
            return false;

        var organs = initialBody.Organs;

        if (part == BodyPartType.Leg && symmetry is null or BodyPartSymmetry.None)
        {
            if (organs.ContainsKey("LegLeft") || organs.ContainsKey("LegRight"))
                return false;

            return organs.ContainsKey("Leg");
        }

        if (part == BodyPartType.Hand && symmetry == BodyPartSymmetry.Left)
        {
            if (organs.ContainsKey("HandLeft") || organs.ContainsKey("HandRight"))
                return false;

            return organs.ContainsKey("Hand");
        }

        if (part == BodyPartType.Foot && symmetry is null or BodyPartSymmetry.None)
        {
            if (organs.ContainsKey("FootLeft") || organs.ContainsKey("FootRight"))
                return false;

            return organs.ContainsKey("Foot");
        }

        if (part == BodyPartType.Tail)
            return organs.ContainsKey("Tail");

        if (!SurgeryBodyPartMapping.TryGetCategory(part, symmetry, out var category))
            return false;

        if (BodyHasArachneOrgan(bodyId)
            && SurgeryBodyPartMapping.IsHumanLegOrFootCategory(category))
            return false;

        return organs.ContainsKey(category);
    }

    /// <summary>
    /// Whether the body currently has an inserted organ marked with <see cref="ArachneOrganComponent"/>.
    /// </summary>
    public bool BodyHasArachneOrgan(EntityUid bodyId, BodyComponent? body = null)
    {
        if (!Resolve(bodyId, ref body, logMissing: false) || body!.Organs == null)
            return false;

        foreach (var organ in body.Organs.ContainedEntities)
        {
            if (HasComp<ArachneOrganComponent>(organ))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Flat-sprite NPCs keep external organs in <see cref="BodyComponent.Organs"/> without
    /// layered <see cref="VisualBodyComponent"/> anatomy.
    /// </summary>
    public bool UsesFlatOrgans(EntityUid bodyId) =>
        HasComp<BodyComponent>(bodyId) && !HasComp<VisualBodyComponent>(bodyId);

    /// <summary>
    /// Arachne grafting requires layered <see cref="VisualBodyComponent"/> humanoids,
    /// not flat-sprite NPC organ sets.
    /// </summary>
    public bool BodySupportsArachneGraft(EntityUid bodyId) =>
        HasComp<VisualBodyComponent>(bodyId);

    /// <summary>
    /// Finds internal organs of a given type hosted under the external organ selected for surgery.
    /// </summary>
    public bool TryGetInternalOrgansForHostPart(
        EntityUid bodyId,
        EntityUid hostPart,
        Type organComponentType,
        [NotNullWhen(true)] out List<(EntityUid Id, OrganComponent Organ)>? organs)
    {
        organs = null;

        if (!TryComp<OrganComponent>(hostPart, out var hostOrgan) || hostOrgan.Category is not { } hostCategory)
            return false;

        if (!TryGetBodyPartOrgans(bodyId, organComponentType, out var all) || all == null)
            return false;

        var filtered = new List<(EntityUid Id, OrganComponent Organ)>();
        foreach (var organ in all)
        {
            if (organ.Organ.Category is not { } category
                || !InternalOrganHostCategory.TryGetValue(category, out var expectedHost)
                || expectedHost != hostCategory)
                continue;

            filtered.Add(organ);
        }

        if (filtered.Count == 0)
            return false;

        organs = filtered;
        return true;
    }

    /// <summary>
    /// Count of inserted human foot organs (<c>FootLeft</c> / <c>FootRight</c>).
    /// </summary>
    public int GetHumanFootCount(EntityUid bodyId, BodyComponent? body = null)
    {
        if (!Resolve(bodyId, ref body, logMissing: false) || body!.Organs == null)
            return 0;

        var count = 0;
        foreach (var organUid in body.Organs.ContainedEntities)
        {
            if (!TryComp<OrganComponent>(organUid, out var organ) || organ.Category is not { } category)
                continue;

            if (SurgeryBodyPartMapping.IsHumanFootCategory(category))
                count++;
        }

        return count;
    }

    public bool HasBothHumanFeet(EntityUid bodyId, BodyComponent? body = null) =>
        GetHumanFootCount(bodyId, body) >= 2;

    /// <summary>
    /// Resolves owning body and part metadata for a woundable organ entity.
    /// </summary>
    public bool TryGetWoundableBodyPartInfo(
        EntityUid woundable,
        out EntityUid bodyUid,
        out BodyPartType partType,
        out BodyPartSymmetry? symmetry)
    {
        bodyUid = default;
        partType = BodyPartType.Chest;
        symmetry = null;

        if (TryComp<OrganComponent>(woundable, out var organ) && organ.Body is { } organBody)
        {
            bodyUid = organBody;
            if (organ.Category is { } category
                && SurgeryBodyPartMapping.TryGetBodyPartType(category, out var mappedType, out var mappedSymmetry))
            {
                partType = mappedType;
                symmetry = mappedSymmetry;
            }

            return true;
        }

        return false;
    }

    /// <summary>
    /// Returns internal organs associated with an external woundable organ on the same body.
    /// </summary>
    public IEnumerable<(EntityUid Id, OrganComponent Component)> GetOrgansForWoundable(EntityUid woundableId)
    {
        if (!TryComp<OrganComponent>(woundableId, out var external))
            yield break;

        var bodyUid = external.Body;
        if (bodyUid is null
            && Containers.TryGetContainingContainer(woundableId, out var container)
            && TryComp<BodyComponent>(container.Owner, out var bodyComp)
            && bodyComp.Organs == container)
        {
            bodyUid = container.Owner;
        }

        if (bodyUid is not { } resolvedBody
            || !TryComp<BodyComponent>(resolvedBody, out var body)
            || external.Category is not { } externalCategory)
            yield break;

        foreach (var organ in GetBodyOrgans(resolvedBody, body))
        {
            if (organ.Component.Category is not { } category || SurgeryBodyPartMapping.IsExternalCategory(category))
                continue;

            if (InternalOrganHostCategory.TryGetValue(category, out var hostCategory) && hostCategory == externalCategory)
                yield return organ;
        }
    }

    /// <summary>
    /// Returns entities that should receive distributed explosion / rejuvenate wound sync damage.
    /// </summary>
    public IEnumerable<EntityUid> GetDistributedDamageTargets(EntityUid bodyId, BodyComponent? body = null)
    {
        foreach (var target in GetWoundableTargets(bodyId, body))
            yield return target;
    }

    /// <summary>
    /// Strips a body down to the head for flesh-cult skeleton conversion.
    /// </summary>
    public void StripBodyForSkeleton(EntityUid bodyId, BodyComponent? body = null)
    {
        if (!Resolve(bodyId, ref body, logMissing: false) || body!.Organs == null)
            return;

        foreach (var organUid in body.Organs.ContainedEntities.ToArray())
        {
            if (!TryComp<OrganComponent>(organUid, out var organ))
            {
                QueueDel(organUid);
                continue;
            }

            if (organ.Category == "Head")
                continue;

            RemoveOrgan(organUid, organ);
            QueueDel(organUid);
        }
    }

    public int GetWoundableTargetCount(EntityUid bodyId, BodyComponent? body = null) =>
        GetWoundableTargets(bodyId, body).Count();

    public bool DestroyWoundable(EntityUid woundableUid, bool gib = false)
    {
        if (!TryComp<OrganComponent>(woundableUid, out var organ) || organ.Body is not { } bodyUid)
            return false;

        if (gib && TryComp<GibbableComponent>(woundableUid, out _))
            _gibbingSystem.TryGibEntity(bodyUid, woundableUid, GibType.Gib, GibContentsOption.Drop, out _);

        RemoveOrgan((woundableUid, organ), bodyUid);
        return true;
    }
}
