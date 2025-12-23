using Content.Server._Backmen.StationEvents.Events;

namespace Content.Server._Backmen.StationEvents.Components;

[RegisterComponent, Access(typeof(MassMindSwapRule))]
public sealed partial class MassMindSwapRuleComponent : Component
{
    /// <summary>
    /// The mind swap is only temporary if true.
    /// </summary>
    [DataField("isTemporary")]
    public bool IsTemporary;
}
