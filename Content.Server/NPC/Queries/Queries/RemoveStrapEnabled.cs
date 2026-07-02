namespace Content.Server.NPC.Queries.Queries;

/// <summary>
/// Remove entities with an enabled strap component from the query.
/// These cannot be picked up via interaction and will buckle the user instead.
/// </summary>
public sealed partial class RemoveStrapEnabledFilter : UtilityQueryFilter
{

}
