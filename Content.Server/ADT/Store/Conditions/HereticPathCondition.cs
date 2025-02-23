using Content.Shared.Heretic;
using Content.Shared.Store;

namespace Content.Server.Store.Conditions;

public sealed partial class HereticPathCondition : ListingCondition
{
    [DataField] public HashSet<HereticPath>? Whitelist;
    [DataField] public HashSet<HereticPath>? Blacklist;
    [DataField] public int Stage = 0;

    public override bool Condition(ListingConditionArgs args)
    {
        var ent = args.EntityManager;

        if (!ent.TryGetComponent<HereticComponent>(args.Buyer, out var hereticComp))
            return false;

        if (Stage > hereticComp.PathStage)
            return false;

        if (Whitelist != null)
        {
            foreach (var white in Whitelist)
            {
                if (hereticComp.CurrentPath == white)
                    return true;
            }
            return false;
        }

        if (Blacklist != null)
        {
            foreach (var black in Blacklist)
            {
                if (hereticComp.CurrentPath == black)
                    return false;
            }
            return true;
        }

        return true;
    }
}
