using Content.Server._Backmen.StationEvents.Events;

namespace Content.Server._Backmen.StationEvents.Components;

[RegisterComponent, Access(typeof(GlimmerRandomSentienceRule))]
public sealed partial class GlimmerRandomSentienceRuleComponent : Component
{
    [DataField("maxMakeSentient")]
    public int MaxMakeSentient = 4;
}
