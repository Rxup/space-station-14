using System.Linq;
using Content.Server.Store.Components;
using Content.Server.Store.Systems;
using Content.Shared.Store;
using Robust.Shared.Prototypes;

namespace Content.Server.Backmen.Vampiric;

public sealed partial class RequireParentListingCondition : ListingCondition
{
    [DataField("parent", required: true)]
    public ProtoId<ListingPrototype> ParentId;
    public override bool Condition(ListingConditionArgs args)
    {
        if (!args.StoreEntity.HasValue)
            return false;

        var parent = args.EntityManager.EnsureComponent<StoreComponent>(args.StoreEntity.Value).Listings.FirstOrDefault(x=>x.ID == ParentId);

        if (parent == null)
            return false;

        return parent.PurchaseAmount > 0;
    }
}
