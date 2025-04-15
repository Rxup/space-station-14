using Robust.Shared.Configuration;

namespace Content.Shared.Backmen.CCVar;

public sealed partial class CCVars
{
    /*
     * Examine
     */
    /// <summary>
    /// Whether or not to log actions in the chat.
    /// </summary>
    public static readonly CVarDef<bool> LogInChat =
        CVarDef.Create("chat.log_in_chat", true, CVar.CLIENT | CVar.ARCHIVE | CVar.REPLICATED);

    /// <summary>
    /// Whether or not to coalesce identical messages in the chat.
    /// </summary>
    public static readonly CVarDef<bool> CoalesceIdenticalMessages =
         CVarDef.Create("chat.coalesce_identical_messages", true, CVar.CLIENT | CVar.ARCHIVE | CVar.CLIENTONLY);

    /// <summary>
    /// Whether or not to show detailed examine text.
    /// </summary>
    public static readonly CVarDef<bool> DetailedExamine =
        CVarDef.Create("misc.detailed_examine", true, CVar.CLIENT | CVar.ARCHIVE | CVar.REPLICATED);
}
