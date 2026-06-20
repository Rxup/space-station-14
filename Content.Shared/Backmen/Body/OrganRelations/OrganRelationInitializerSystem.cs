using Content.Shared.Body;
using Content.Shared.Body.Events;
using Content.Shared.Body.Organ;
using Robust.Shared.Prototypes;
using System.Linq;

namespace Content.Shared.Backmen.Body.OrganRelations;

/// <summary>
/// Wires nubody organ parent/child relations after initial body spawn.
/// </summary>
public sealed class OrganRelationInitializerSystem : EntitySystem
{
    [Dependency] private BodySystem _body = default!;
    [Dependency] private OrganRelationSystem _organRelation = default!;

    public static readonly Dictionary<ProtoId<OrganCategoryPrototype>, HashSet<ProtoId<OrganCategoryPrototype>>> StandardRelationships =
        new()
        {
            {
                "Torso",
                ["Head", "ArmLeft", "ArmRight", "LegLeft", "LegRight", "Appendix", "Lungs", "Heart", "Stomach", "Liver", "Kidneys"]
            },
            { "Head", ["Brain", "Eyes", "Tongue", "Ears"] },
            { "ArmLeft", ["HandLeft"] },
            { "ArmRight", ["HandRight"] },
            { "LegLeft", ["FootLeft"] },
            { "LegRight", ["FootRight"] },
        };

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<InitialBodyComponent, InitialBodySpawnedEvent>(OnInitialBodySpawned);
    }

    private void OnInitialBodySpawned(Entity<InitialBodyComponent> ent, ref InitialBodySpawnedEvent args)
    {
        if (!TryComp<BodyComponent>(ent, out var body))
            return;

        WireRelationships((ent, body), ent.Comp.Relationships);
    }

    public void WireRelationships(
        Entity<BodyComponent> bodyEnt,
        Dictionary<ProtoId<OrganCategoryPrototype>, HashSet<ProtoId<OrganCategoryPrototype>>>? relationships = null)
    {
        relationships ??= StandardRelationships;

        var byCategory = new Dictionary<ProtoId<OrganCategoryPrototype>, EntityUid>();
        foreach (var organ in _body.GetOrgansByCategories(bodyEnt.AsNullable(), relationships.Keys))
        {
            if (organ.Comp.Category is { } category)
                byCategory[category] = organ.Owner;
        }

        foreach (var childCategory in relationships.Values.SelectMany(x => x))
        {
            if (byCategory.ContainsKey(childCategory))
                continue;

            if (_body.TryGetOrganByCategory(bodyEnt.AsNullable(), childCategory, out var childOrgan)
                && childOrgan.Comp.Category is { } cat)
                byCategory[cat] = childOrgan.Owner;
        }

        foreach (var (parentCategory, children) in relationships)
        {
            if (!byCategory.TryGetValue(parentCategory, out var parentUid))
                continue;

            foreach (var childCategory in children)
            {
            if (!byCategory.TryGetValue(childCategory, out var childUid))
                continue;

            if (TryComp<ChildOrganComponent>(childUid, out var childComp) && childComp.Parent == parentUid)
                continue;

            EnsureComp<ParentOrganComponent>(parentUid);
            EnsureComp<ChildOrganComponent>(childUid);
            _organRelation.Relate(parentUid, childUid);
            }
        }

        WireGraftRelationships(bodyEnt, byCategory);
    }

    /// <summary>
    /// Links grafted arachne organs to humanoid (and other) bodies after surgery or map init.
    /// </summary>
    public void WireGraftRelationships(
        Entity<BodyComponent> bodyEnt,
        Dictionary<ProtoId<OrganCategoryPrototype>, EntityUid>? byCategory = null)
    {
        byCategory ??= BuildCategoryMap(bodyEnt);

        foreach (var (childCategory, parentCategory) in GraftParentCategories)
        {
            if (!byCategory.TryGetValue(childCategory, out var childUid)
                || !byCategory.TryGetValue(parentCategory, out var parentUid))
                continue;

            if (TryComp<ChildOrganComponent>(childUid, out var childComp) && childComp.Parent == parentUid)
                continue;

            EnsureComp<ParentOrganComponent>(parentUid);
            EnsureComp<ChildOrganComponent>(childUid);
            _organRelation.Relate(parentUid, childUid);
        }
    }

    private Dictionary<ProtoId<OrganCategoryPrototype>, EntityUid> BuildCategoryMap(Entity<BodyComponent> bodyEnt)
    {
        var byCategory = new Dictionary<ProtoId<OrganCategoryPrototype>, EntityUid>();
        foreach (var organ in _body.GetOrgansByCategories(bodyEnt.AsNullable(), GraftParentCategories.Keys))
        {
            if (organ.Comp.Category is { } category)
                byCategory[category] = organ.Owner;
        }

        foreach (var organ in _body.GetOrgansByCategories(bodyEnt.AsNullable(), GraftParentCategories.Values))
        {
            if (organ.Comp.Category is { } category)
                byCategory[category] = organ.Owner;
        }

        return byCategory;
    }

    public static readonly Dictionary<ProtoId<OrganCategoryPrototype>, ProtoId<OrganCategoryPrototype>> GraftParentCategories =
        new()
        {
            { "ArachneAbdomen", "Torso" },
            { "ArachneFront", "Torso" },
            { "SpiderLegLeft1", "ArachneAbdomen" },
            { "SpiderLegLeft2", "ArachneAbdomen" },
            { "SpiderLegLeft3", "ArachneAbdomen" },
            { "SpiderLegLeft4", "ArachneAbdomen" },
            { "SpiderLegRight1", "ArachneFront" },
            { "SpiderLegRight2", "ArachneFront" },
            { "SpiderLegRight3", "ArachneFront" },
            { "SpiderLegRight4", "ArachneFront" },
        };
}
