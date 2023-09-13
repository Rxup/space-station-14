using Content.Server.Backmen.StationEvents.Events;

namespace Content.Server.Backmen.StationEvents.Components;

[RegisterComponent, Access(typeof(NoosphericStormRule))]
public sealed partial class NoosphericStormRuleComponent : Component
{
    /// <summary>
    /// How many potential psionics should be awakened at most.
    /// </summary>
    [DataField("maxAwaken")]
    public int MaxAwaken { get; private set; } = 3;

    /// <summary>
    /// </summary>
    [DataField("baseGlimmerAddMin")]
    public int BaseGlimmerAddMin { get; private set; } = 65;

    /// <summary>
    /// </summary>
    [DataField("baseGlimmerAddMax")]
    public int BaseGlimmerAddMax { get; private set; } = 85;

    /// <summary>
    /// Multiply the EventSeverityModifier by this to determine how much extra glimmer to add.
    /// </summary>
    [DataField("glimmerSeverityCoefficient")]
    public float GlimmerSeverityCoefficient { get; private set; }= 0.25f;
}
