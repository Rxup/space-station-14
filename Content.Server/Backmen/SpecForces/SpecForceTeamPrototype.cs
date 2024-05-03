using Content.Shared.Storage;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.Backmen.SpecForces;

[Prototype("specForceTeam")]
public sealed partial class SpecForceTeamPrototype : IPrototype
{
    [ViewVariables]
    [IdDataField]
    public string ID { get; } = default!;
    /// <summary>
    /// Name of the SpecForceTeam that will be shown at the round end manifest.
    /// </summary>
    [ViewVariables]
    [DataField("specForceName", required: true)]
    public readonly LocId SpecForceName;
    /// <summary>
    /// Shuttle path for the SpecForce.
    /// </summary>
    [ViewVariables]
    [DataField("shuttlePath", required: true)]
    public readonly string ShuttlePath = default!;
    /// <summary>
    /// Announcement text for the SpecForce.
    /// </summary>
    [ViewVariables]
    [DataField("spawnMarker", required: true)]
    public readonly EntProtoId SpawnMarker;
    /// <summary>
    /// Announcement text for the SpecForce.
    /// </summary>
    [ViewVariables]
    [DataField("announcementText")]
    public readonly LocId? AnnouncementText;
    /// <summary>
    /// Announcement title for the SpecForce.
    /// </summary>
    [ViewVariables]
    [DataField("announcementTitle")]
    public readonly LocId? AnnouncementTitle;
    /// <summary>
    /// Announcement sound for the SpecForce.
    /// </summary>
    [ViewVariables]
    [DataField("announcementSoundPath")]
    public readonly SoundSpecifier? AnnouncementSoundPath = default!;
    /// <summary>
    /// На какое количество игроков будет приходиться спавн ещё одной гост роли.
    /// По умолчанию: за каждого 10-го игрока прибавляется 1 гост роль
    /// </summary>
    [ViewVariables]
    [DataField("spawnPerPlayers")]
    public readonly int SpawnPerPlayers = 10;
    /// <summary>
    /// Max amount of ghost roles that can be spawned.
    /// </summary>
    [ViewVariables]
    [DataField("maxRolesAmount")]
    public readonly int MaxRolesAmount = 8;
    /// <summary>
    /// SpecForces that will be spawned no matter what.
    /// Uses EntitySpawnEntry and therefore has ability to change spawn prob.
    /// </summary>
    [ViewVariables]
    [DataField("guaranteedSpawn")]
    public readonly List<EntitySpawnEntry> GuaranteedSpawn = default!;
    /// <summary>
    /// SpecForces that will be spawned using the spawnPerPlayers variable.
    /// Ghost roles will spawn by the order they arranged in list.
    /// </summary>
    [ViewVariables]
    [DataField("specForceSpawn")]
    public readonly List<EntitySpawnEntry> SpecForceSpawn = default!;
}
