using Content.Server.Power.Components;

namespace Content.Server.Backmen.StationEvents.Components;

[RegisterComponent]
public sealed partial class GlimmerBreakerRuleComponent : Component
{
    [DataField]
    public int Min { get; set; } = 1;
    [DataField]
    public int Max { get; set; } = 3;
    [DataField]
    public int DurationSeconds { get; set; } = 10;

    public List<Entity<ApcComponent>> AffectedApc = [];
    public TimeSpan NextPulse = TimeSpan.Zero;
}
