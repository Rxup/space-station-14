using Content.Server.StationEvents;
using Content.Server.StationEvents.Components;
using Content.Shared.EntityTable.EntitySelectors;

namespace Content.Server.Backmen.Psionics.Glimmer.Components;

/// <summary>
/// Компонент для управления событиями, основанными на уровне сияния.
/// </summary>
[RegisterComponent]
public sealed partial class GlimmerStationEventSchedulerComponent : Component
{
    /// <summary>
    /// Время до следующего события (в секундах).
    /// </summary>
    [DataField("timeUntilNextEvent")]
    public float TimeUntilNextEvent = 0f;

    /// <summary>
    /// Минимальное время до следующего события (в секундах).
    /// </summary>
    [DataField("minEventInterval")]
    public float MinEventInterval = 120f;

    /// <summary>
    /// Максимальное время до следующего события (в секундах).
    /// </summary>
    [DataField("maxEventInterval")]
    public float MaxEventInterval = 600f;

    /// <summary>
    /// Игровые правила, которые планировщик может выбрать.
    /// </summary>
    [DataField(required: true)]
    public EntityTableSelector ScheduledGameRules = default!;
}
