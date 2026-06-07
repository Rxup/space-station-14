using Content.Server.NPC.Queries.Queries;

namespace Content.Server.Backmen.NPC.Queries.Queries;

/// <summary>
/// Returns nearby mobs that may be cocooned, including sleeping non-hostiles.
/// </summary>
public sealed partial class NearbyCocoonVictimsQuery : UtilityQuery;
