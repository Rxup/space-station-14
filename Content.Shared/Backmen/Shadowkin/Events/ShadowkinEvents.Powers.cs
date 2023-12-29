using Content.Shared.Actions;
using Content.Shared.Magic;
using Robust.Shared.Audio;

namespace Content.Server.Backmen.Species.Shadowkin.Events;

/// <summary>
///     Raised when the shadowkin teleport action is used.
/// </summary>
public sealed partial class ShadowkinTeleportEvent : WorldTargetActionEvent, ISpeakSpell
{
    [DataField("sound")]
    public SoundSpecifier Sound = new SoundPathSpecifier("/Audio/Backmen/Effects/Shadowkin/Powers/teleport.ogg");

    [DataField("volume")]
    public float Volume = 5f;


    [DataField("powerCost")]
    public float PowerCost = 40f;

    [DataField("staminaCost")]
    public float StaminaCost = 20f;


    [DataField("speech"), ViewVariables(VVAccess.ReadOnly)]
    public string? Speech { get; set; }
}

/// <summary>
///     Raised when the shadowkin darkSwap action is used.
/// </summary>
public sealed partial class ShadowkinDarkSwapEvent : InstantActionEvent, ISpeakSpell
{
    [DataField("soundOn")]
    public SoundSpecifier SoundOn = new SoundPathSpecifier("/Audio/Backmen/Effects/Shadowkin/Powers/darkswapon.ogg");

    [DataField("volumeOn")]
    public float VolumeOn = 5f;

    [DataField("soundOff")]
    public SoundSpecifier SoundOff = new SoundPathSpecifier("/Audio/Backmen/Effects/Shadowkin/Powers/darkswapoff.ogg");

    [DataField("volumeOff")]
    public float VolumeOff = 5f;


    /// <summary>
    ///     How much stamina to drain when darkening.
    /// </summary>
    [DataField("powerCostOn")]
    public float PowerCostOn = 60f;

    /// <summary>
    ///     How much stamina to drain when lightening.
    /// </summary>
    [DataField("powerCostOff")]
    public float PowerCostOff = 45f;

    /// <summary>
    ///     How much stamina to drain when darkening.
    /// </summary>
    [DataField("staminaCostOn")]
    public float StaminaCostOn;

    /// <summary>
    ///     How much stamina to drain when lightening.
    /// </summary>
    [DataField("staminaCostOff")]
    public float StaminaCostOff;


    [DataField("speech")]
    public string? Speech { get; set; }
}

public sealed class ShadowkinDarkSwapAttemptEvent : CancellableEntityEventArgs
{
    NetEntity Performer;

    public ShadowkinDarkSwapAttemptEvent(NetEntity performer)
    {
        Performer = performer;
    }
}


public sealed partial class ShadowkinRestEvent: InstantActionEvent
{

}
