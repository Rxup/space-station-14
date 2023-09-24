using Content.Server.Backmen.StationEvents.Events;

namespace Content.Server.Backmen.StationEvents.Components;

[RegisterComponent, Access(typeof(GlimmerRandomSentienceRule))]
public sealed partial class GlimmerRandomSentienceRuleComponent : Component
{
    [DataField("maxMakeSentient")]
    public int MaxMakeSentient = 4;
}
