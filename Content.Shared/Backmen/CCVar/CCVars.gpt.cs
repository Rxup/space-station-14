using Robust.Shared.Configuration;

namespace Content.Shared.Backmen.CCVar;

public sealed partial class CCVars
{
    /*
     * GPT
     */
    public static readonly CVarDef<bool>
        GptEnabled = CVarDef.Create("gpt.enabled", false, CVar.SERVERONLY);

    public static readonly CVarDef<string>
        GptModel = CVarDef.Create("gpt.model", "gpt-3.5-turbo-0613", CVar.SERVERONLY);

    public static readonly CVarDef<string>
        GptApiUrl = CVarDef.Create("gpt.api", "https://api.openai.com/v1/", CVar.SERVERONLY | CVar.CONFIDENTIAL);

    public static readonly CVarDef<string>
        GptApiToken = CVarDef.Create("gpt.token", "", CVar.SERVERONLY | CVar.CONFIDENTIAL);

    public static readonly CVarDef<string>
        GptApiGigaToken = CVarDef.Create("gpt.giga_token", "", CVar.SERVERONLY | CVar.CONFIDENTIAL);

    public static readonly CVarDef<bool>
        GptApiNoAdminAuto = CVarDef.Create("gpt.no_admin_auto", false, CVar.SERVERONLY | CVar.CONFIDENTIAL);
}
