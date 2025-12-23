using Content.Server._Backmen.StationEvents.Events;
using Content.Shared.FixedPoint;

namespace Content.Server._Backmen.StationEvents.Components;

[RegisterComponent, Access(typeof(FreeProberRule))]
public sealed partial class FreeProberRuleComponent : Component
{
    [DataField("afterGlimmerExtra")]
    public int AfterGlimmerExtra = 500;

    [DataField("propExtra")]
    public FixedPoint2 PropExtra = 0.25;
}
