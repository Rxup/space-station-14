using Content.Server._Backmen.StationEvents.Events;

namespace Content.Server._Backmen.StationEvents.Components;

[RegisterComponent, Access(typeof(GlimmerRevenantRule))]
public sealed partial class GlimmerRevenantRuleComponent : Component
{
    [DataField("prototype")] public string RevenantPrototype { get; private set; } = "MobRevenant";
}
