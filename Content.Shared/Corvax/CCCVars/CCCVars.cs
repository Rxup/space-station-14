﻿using Robust.Shared.Configuration;

namespace Content.Shared.Corvax.CCCVars;

/// <summary>
///     Corvax modules console variables
/// </summary>
[CVarDefs]
// ReSharper disable once InconsistentNaming
public sealed class CCCVars
{
    /// <summary>
    /// Deny any VPN connections.
    /// </summary>
    public static readonly CVarDef<bool> PanicBunkerDenyVPN =
        CVarDef.Create("game.panic_bunker.deny_vpn", false, CVar.SERVERONLY);

    /**
     * TTS (Text-To-Speech)
     */

    /// <summary>
    /// URL of the TTS server API.
    /// </summary>
    public static readonly CVarDef<bool> TTSEnabled =
        CVarDef.Create("tts.enabled", false, CVar.SERVER | CVar.REPLICATED | CVar.ARCHIVE);

    /// <summary>
    /// URL of the TTS server API.
    /// </summary>
    public static readonly CVarDef<string> TTSApiUrl =
        CVarDef.Create("tts.api_url", "", CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>
    /// Auth token of the TTS server API.
    /// </summary>
    public static readonly CVarDef<string> TTSApiToken =
        CVarDef.Create("tts.api_token", "", CVar.SERVERONLY | CVar.CONFIDENTIAL);

    /// <summary>
    /// Amount of seconds before timeout for API
    /// </summary>
    public static readonly CVarDef<int> TTSApiTimeout =
        CVarDef.Create("tts.api_timeout", 5, CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>
    /// Default volume setting of TTS sound
    /// </summary>
    public static readonly CVarDef<float> TTSVolume =
        CVarDef.Create("tts.volume", 0f, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Count of in-memory cached tts voice lines.
    /// </summary>
    public static readonly CVarDef<int> TTSMaxCache =
        CVarDef.Create("tts.max_cache", 250, CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>
    /// VoiceId for Announcement TTS
    /// </summary>
    public static readonly CVarDef<string> TTSAnnounceVoiceId =
        CVarDef.Create("tts.announce_voice", "Announcer", CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>
    /// Default volume setting of TTS Announce sound
    /// </summary>
    public static readonly CVarDef<float> TTSAnnounceVolume =
        CVarDef.Create("tts.announce_volume", 0f, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// Tts rate limit values are accounted in periods of this size (seconds).
    /// After the period has passed, the count resets.
    /// </summary>
    public static readonly CVarDef<float> TTSRateLimitPeriod =
        CVarDef.Create("tts.rate_limit_period", 2f, CVar.SERVERONLY);

    /// <summary>
    /// How many tts preview messages are allowed in a single rate limit period.
    /// </summary>
    public static readonly CVarDef<int> TTSRateLimitCount =
        CVarDef.Create("tts.rate_limit_count", 3, CVar.SERVERONLY);

    /*
     * Peaceful Round End
     */

    /// <summary>
    /// Making everyone a pacifist at the end of a round.
    /// </summary>
    public static readonly CVarDef<bool> PeacefulRoundEnd =
        CVarDef.Create("game.peaceful_end", true, CVar.SERVERONLY);

    /*
     * Station Goal
     */

    /// <summary>
    /// Send station goal on round start or not.
    /// </summary>
    public static readonly CVarDef<bool> StationGoal =
        CVarDef.Create("game.station_goal", true, CVar.SERVERONLY);
}
