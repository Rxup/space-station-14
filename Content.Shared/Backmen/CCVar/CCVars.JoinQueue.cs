using Robust.Shared.Configuration;

namespace Content.Shared.Backmen.CCVar;

public sealed partial class CCVars
{
    /*
     * Queue
     */

    /// <summary>
    ///     Controls if the connections queue is enabled. If enabled stop kicking new players after `SoftMaxPlayers` cap and instead add them to queue.
    /// </summary>
    public static readonly CVarDef<bool>
        QueueEnabled = CVarDef.Create("queue.enabled", false, CVar.SERVERONLY);

    public static readonly CVarDef<bool>
        QueueAltEnabled = CVarDef.Create("queue.alt_servers", false, CVar.SERVERONLY);
}
