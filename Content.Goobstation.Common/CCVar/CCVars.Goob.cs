
    #region Voicechat

    /// <summary>
    /// Controls whether the Lidgren voice chat server is enabled and running.
    /// </summary>
    public static readonly CVarDef<bool> VoiceChatEnabled =
        CVarDef.Create("voice.enabled", false, CVar.SERVER | CVar.REPLICATED | CVar.ARCHIVE, "Is the voice chat server enabled?");

    /// <summary>
    /// The UDP port the Lidgren voice chat server will listen on.
    /// </summary>
    public static readonly CVarDef<int> VoiceChatPort =
        CVarDef.Create("voice.vc_server_port", 1213, CVar.SERVER | CVar.REPLICATED, "Port for the voice chat server.");

    public static readonly CVarDef<float> VoiceChatVolume =
        CVarDef.Create("voice.volume", 5f, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Multiplier for the adaptive buffer target size calculation.
    /// </summary>
    public static readonly CVarDef<float> VoiceChatBufferTargetMultiplier =
        CVarDef.Create("voice.buffer_target_multiplier", 1.0f, CVar.CLIENTONLY | CVar.ARCHIVE, "Multiplier for adaptive buffer target size calculation.");

    /// <summary>
    /// Minimum buffer size for voice chat, regardless of network conditions.
    /// </summary>
    public static readonly CVarDef<int> VoiceChatMinBufferSize =
        CVarDef.Create("voice.min_buffer_size", 10, CVar.CLIENTONLY | CVar.ARCHIVE, "Minimum buffer size for voice chat.");

    /// <summary>
    /// Maximum buffer size for voice chat to prevent excessive memory usage.
    /// </summary>
    public static readonly CVarDef<int> VoiceChatMaxBufferSize =
        CVarDef.Create("voice.max_buffer_size", 50, CVar.CLIENTONLY | CVar.ARCHIVE, "Maximum buffer size for voice chat.");

    /// <summary>
    /// Enable advanced time-stretching algorithms for better audio quality.
    /// </summary>
    public static readonly CVarDef<bool> VoiceChatAdvancedTimeStretch =
        CVarDef.Create("voice.advanced_time_stretch", true, CVar.CLIENTONLY | CVar.ARCHIVE, "Enable advanced time-stretching for voice chat.");

    /// <summary>
    /// Enable debug logging for voice chat buffer management.
    /// </summary>
    public static readonly CVarDef<bool> VoiceChatDebugLogging =
        CVarDef.Create("voice.debug_logging", false, CVar.CLIENTONLY | CVar.ARCHIVE, "Enable debug logging for voice chat buffer management.");

    /// <summary>
    /// Whether to hear audio from your own entity (useful for testing).
    /// </summary>
    public static readonly CVarDef<bool> VoiceChatHearSelf =
        CVarDef.Create("voice.hear_self", false, CVar.CLIENTONLY | CVar.ARCHIVE, "Whether to hear audio from your own entity.");

    #endregion
