using Content.Server.NPC.Queries.Considerations;

namespace Content.Server.Backmen.NPC.Queries.Considerations;

/// <summary>
/// Excludes incapacitated targets that should be cocooned instead of attacked.
/// </summary>
public sealed partial class NotCocoonVictimCon : UtilityConsideration
{
}
