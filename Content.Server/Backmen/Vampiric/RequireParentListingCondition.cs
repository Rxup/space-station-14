using System.Linq;
using Content.Shared.Store;
using Content.Shared.Store.Components;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;

namespace Content.Server.Backmen.Vampiric;

[MeansImplicitUse]
public sealed partial class RequireParentListingCondition : ListingCondition
{
    [DataField("parent", required: true)]
    public ProtoId<ListingPrototype> ParentId;

    public override bool Condition(ListingConditionArgs args)
    {
        var parent = args.EntityManager
            .GetComponentOrNull<StoreComponent>(args.StoreEntity)
            ?.FullListingsCatalog
            .FirstOrDefault(x => ParentId == x.ID);

        if (parent == null)
            return false;

        return parent.PurchaseAmount > 0;
    }
}
