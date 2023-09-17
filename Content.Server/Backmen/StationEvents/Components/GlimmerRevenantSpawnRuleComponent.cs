using Content.Server.Backmen.StationEvents.Events;

namespace Content.Server.Backmen.StationEvents.Components;

[RegisterComponent, Access(typeof(GlimmerRevenantRule))]
public sealed partial class GlimmerRevenantRuleComponent : Component
{
    [DataField("prototype")] public string RevenantPrototype { get; private set; } = "MobRevenant";
}
