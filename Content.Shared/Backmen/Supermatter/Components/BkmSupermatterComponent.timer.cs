namespace Content.Shared.Backmen.Supermatter.Components;

public partial class BkmSupermatterComponent
{
        /// <summary>
    /// The point at which we should start sending messeges
    /// about the damage to the engi channels.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    [DataField("WarningPoint")]
    public float WarningPoint = 50;

    /// <summary>
    /// The point at which we start sending messages to the common channel
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    [DataField("emergencyPoint")]
    public float EmergencyPoint = 500;

    /// <summary>
    /// we yell if over 50 damage every YellTimer Seconds
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public float YellTimer = 30f;

    /// <summary>
    /// set to YellTimer at first so it doesnt yell a minute after being hit
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public float YellAccumulator = 30f;

    /// <summary>
    /// YellTimer before the SM is about the delam
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public float YellDelam = 5f;

    /// <summary>
    ///  Timer for Damage
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public float DamageUpdateAccumulator;

    /// <summary>
    /// update environment damage every 1 second
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public float DamageUpdateTimer = 1f;

    /// <summary>
    /// Timer for delam
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public float DelamTimerAccumulator;

    /// <summary>
    ///     Time until delam
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField]
    public float DelamTimer = 120f;

    /// <summary>
    ///  The message timer
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public float SpeakAccumulator = 5f;

    /// <summary>
    /// Atmos update timer
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public float AtmosUpdateAccumulator;

    /// <summary>
    /// update atmos every 1 second
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public float AtmosUpdateTimer = 1f;

    [DataField]
    public float ZapAccumulator = 0f;

    [DataField]
    public float ZapTimer = 10f;
}
