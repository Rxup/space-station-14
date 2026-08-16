using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server.Backmen.Research;

/// <summary>
/// Dumps waste heat into the surrounding atmosphere while the R&amp;D server is powered.
/// Heat scales with actual received power.
/// </summary>
[RegisterComponent]
public sealed partial class ResearchServerHeatProducingComponent : Component
{
    /// <summary>
    /// Multiplier from received watts to joules of heat per second.
    /// 1 means all consumed power becomes waste heat.
    /// </summary>
    [DataField]
    public float HeatScale = 1f;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan NextSecond;
}
