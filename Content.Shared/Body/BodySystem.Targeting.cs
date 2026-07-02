using Robust.Shared.Prototypes;

namespace Content.Shared.Body;

public sealed partial class BodySystem
{
    /// <summary>
    /// Finds the first organ in the flat body container with the given category.
    /// </summary>
    public bool TryGetOrganByCategory(
        Entity<BodyComponent?> ent,
        ProtoId<OrganCategoryPrototype> category,
        out Entity<OrganComponent> organ)
    {
        organ = default;

        if (!_bodyQuery.Resolve(ent, ref ent.Comp))
            return false;

        foreach (var contained in ent.Comp.Organs?.ContainedEntities ?? [])
        {
            if (TerminatingOrDeleted(contained))
                continue;

            if (!_organQuery.TryComp(contained, out var organComp) || organComp.Category != category)
                continue;

            organ = (contained, organComp);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Returns all organs in the flat body container matching any of the given categories.
    /// </summary>
    public IEnumerable<Entity<OrganComponent>> GetOrgansByCategories(
        Entity<BodyComponent?> ent,
        IEnumerable<ProtoId<OrganCategoryPrototype>> categories)
    {
        if (!_bodyQuery.Resolve(ent, ref ent.Comp))
            yield break;

        var categorySet = categories as HashSet<ProtoId<OrganCategoryPrototype>>
                          ?? new HashSet<ProtoId<OrganCategoryPrototype>>(categories);

        foreach (var contained in ent.Comp.Organs?.ContainedEntities ?? [])
        {
            if (!_organQuery.TryComp(contained, out var organComp) || organComp.Category is not { } cat)
                continue;

            if (categorySet.Contains(cat))
                yield return (contained, organComp);
        }
    }

    /// <summary>
    /// Assigns an organ category (used when grafting generic spider legs into a specific slot).
    /// </summary>
    public void SetOrganCategory(EntityUid organ, ProtoId<OrganCategoryPrototype>? category)
    {
        if (!_organQuery.TryComp(organ, out var organComp))
            return;

        organComp.Category = category;
        Dirty(organ, organComp);
    }
}
