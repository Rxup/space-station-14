using Content.Shared.Polymorph;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server.Backmen.Boss;

[RegisterComponent]
[AutoGenerateComponentPause]
public sealed partial class TimedPolymorphComponent : Component
{
    [DataField]
    public List<ProtoId<PolymorphPrototype>> Prototypes = [];

    /// <summary>
    /// Chance of an entity being spawned at the end of each interval.
    /// </summary>
    [DataField]
    public float Chance = 1.0f;

    /// <summary>
    /// Length of the interval between spawn attempts.
    /// </summary>
    [DataField]
    public TimeSpan IntervalSeconds = TimeSpan.FromSeconds(60);

    /// <summary>
    /// The time at which the current interval will have elapsed and entities may be spawned.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextFire = TimeSpan.Zero;

    [DataField]
    public bool InCombatOnly = true;
}
