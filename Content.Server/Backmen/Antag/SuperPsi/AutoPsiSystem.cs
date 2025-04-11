using System.Linq;
using Content.Server.Access.Systems;
using Content.Server.Antag;
using Content.Server.Backmen.Fugitive;
using Content.Server.Forensics;
using Content.Server.IdentityManagement;
using Content.Server.RandomMetadata;
using Content.Server.Salvage.Expeditions;
using Content.Server.Shuttles.Systems;
using Content.Server.Spawners.Components;
using Content.Server.Spawners.EntitySystems;
using Content.Server.Station.Systems;
using Content.Server.StationRecords.Systems;
using Content.Server.Storage.Components;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Cargo.Components;
using Content.Shared.CriminalRecords;
using Content.Shared.Forensics.Components;
using Content.Shared.Humanoid;
using Content.Shared.Inventory;
using Content.Shared.Mind.Components;
using Content.Shared.PDA;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Content.Shared.Roles.Jobs;
using Content.Shared.StationRecords;
using Content.Shared.Wall;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Backmen.Antag.SuperPsi;

public sealed class AutoPsiSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly AntagSelectionSystem _antag = default!;
    [Dependency] private readonly StationSpawningSystem _stationSpawning = default!;
    [Dependency] private readonly StationSystem _stationSystem = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IdentitySystem _identity = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly RandomMetadataSystem _randomMetadata = default!;
    [Dependency] private readonly StationRecordsSystem _recordsSystem = default!;
    [Dependency] private readonly IdCardSystem _idCardSystem = default!;
    [Dependency] private readonly AccessReaderSystem _accessReader = default!;
    [Dependency] private readonly ISharedPlayerManager _playerMgr = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<AutoPsiComponent, MindAddedMessage>(OnMindAdded);
        SubscribeLocalEvent<SuperPsiRuleComponent, AfterAntagEntitySelectedEvent>(OnSelectAntag);

        SubscribeLocalEvent<PlayerSpawningEvent>(HandlePlayerSpawning,
            before: new[]
            {
                typeof(ContainerSpawnPointSystem),
                typeof(SpawnPointSystem),
                typeof(ArrivalsSystem),
                typeof(SpawnPointSystem),
                typeof(FugitiveSystem)
            });
    }

    [ValidatePrototypeId<JobPrototype>]
    private const string JobPrisoner = "Prisoner";

    public IEnumerable<(EntityCoordinates Pos, EntityUid? Marker)> GetPrisonersSpawningEntities(EntityUid? stationId)
    {
        var points = EntityQueryEnumerator<SpawnPointComponent, TransformComponent>();

        var doGenerator = true;

        while (points.MoveNext(out var uid, out var spawnPoint, out var xform))
        {
            if (stationId != null && _stationSystem.GetOwningStation(uid, xform) != stationId)
                continue;
            if (xform.GridUid == null)
                continue;
            if (HasComp<CargoShuttleComponent>(xform.GridUid) || HasComp<SalvageShuttleComponent>(xform.GridUid))
                continue;
            if (spawnPoint.SpawnType == SpawnPointType.Job &&
                spawnPoint.Job == JobPrisoner)
            {
                doGenerator = false;
                yield return (xform.Coordinates, uid);
            }
        }

        if(!doGenerator)
            yield break;

        var points2 = EntityQueryEnumerator<EntityStorageComponent, TransformComponent, MetaDataComponent>();

        while (points2.MoveNext(out var uid, out _, out var xform, out var spawnPoint))
        {
            if (stationId != null && _stationSystem.GetOwningStation(uid, xform) != stationId)
                continue;

            if (xform.GridUid == null)
                continue;
            if (HasComp<CargoShuttleComponent>(xform.GridUid) || HasComp<SalvageShuttleComponent>(xform.GridUid))
                continue;

            if (spawnPoint.EntityPrototype?.ID is "WardrobePrison" or "WardrobePrisonFilled" or "ClosetWallOrange")
            {
                if (HasComp<WallMountComponent>(uid))
                {
                    var pos = xform.Coordinates.WithPosition((xform.LocalPosition +
                                                              xform.LocalRotation.ToWorldVec() * 1f));
                    yield return (pos, null);
                    continue;
                }

                yield return (xform.Coordinates, null);
            }
        }
    }

    private void HandlePlayerSpawning(PlayerSpawningEvent args)
    {
        if (args.SpawnResult != null)
            return;

        if (!(args.Job != null &&
              _prototypeManager.TryIndex(args.Job, out var jobInfo) &&
              jobInfo.AlwaysUseSpawner))
        {
            return;
        }

        if(jobInfo.ID != JobPrisoner)
            return;



        var spawnLoc = _random.Pick(GetPrisonersSpawningEntities(args.Station).ToList());

        if (_random.Prob(
#if DEBUG
                1f
#else
               0.35f
#endif
            )
#if !DEBUG
            && _playerMgr.PlayerCount > 20
#endif
            )
        {
            // do super psi?
            var rule = _antag.ForceGetGameRuleEnt<SuperPsiRuleComponent>(DefaultSuperPsiRule);
            if (_antag.GetTargetAntagCount(rule) > rule.Comp.AssignedMinds.Count)
            {
                args.SpawnResult = SpawnSuperPsi(
                    spawnLoc.Pos,
                    jobInfo,
                    args.HumanoidCharacterProfile,
                    args.Station);

                return;
            }
        }

        args.SpawnResult = _stationSpawning.SpawnPlayerMob(spawnLoc.Pos, args.Job, args.HumanoidCharacterProfile, args.Station);
    }

    [ValidatePrototypeId<EntityPrototype>]
    private const string JobPrisonerSuperPsi = "UristMcNars";
    private EntityUid? SpawnSuperPsi(EntityCoordinates coordinates, JobPrototype job, HumanoidCharacterProfile? profile, EntityUid? station)
    {
        var ent = Spawn(JobPrisonerSuperPsi, coordinates);

        if (TryComp<RandomMetadataComponent>(ent, out var component))
        {
            var meta = MetaData(ent);

            if (component.NameSegments != null)
            {
                _metaData.SetEntityName(ent, _randomMetadata.GetRandomFromSegments(component.NameSegments, component.NameFormat), meta);
            }

            if (component.DescriptionSegments != null)
            {
                _metaData.SetEntityDescription(ent,
                    _randomMetadata.GetRandomFromSegments(component.DescriptionSegments, component.DescriptionFormat), meta);
            }
            RemComp(ent, component);
        }

        if (job.StartingGear != null)
        {
            var startingGear = _prototypeManager.Index<StartingGearPrototype>(job.StartingGear);
            _stationSpawning.EquipStartingGear(ent, startingGear, raiseEvent: false);
        }
        _stationSpawning.SetPdaAndIdCardData(ent, Name(ent), job, station);
        foreach (var jobSpecial in job.Special)
        {
            jobSpecial.AfterEquip(ent);
        }
        _identity.QueueIdentityUpdate(ent);

        return ent;
    }

    private void OnSelectAntag(Entity<SuperPsiRuleComponent> ent, ref AfterAntagEntitySelectedEvent args)
    {
        if (!TryComp<HumanoidAppearanceComponent>(args.EntityUid, out var humanoidAppearanceComponent))
            return;

        var station = _stationSystem.GetOwningStation(args.EntityUid);

        TryComp<DnaComponent>(args.EntityUid, out var dnaComponent);
        TryComp<FingerprintComponent>(args.EntityUid, out var fingerprintComponent);

        if (_accessReader.FindStationRecordKeys(args.EntityUid, out var recordKeys))
        {
            foreach (var recordKey in recordKeys)
            {
                if (_recordsSystem.TryGetRecord<GeneralStationRecord>(recordKey, out var record))
                {
                    record.Name = Name(args.EntityUid);
                    record.Age = humanoidAppearanceComponent.Age;
                    record.Species = humanoidAppearanceComponent.Species;
                    record.Gender = humanoidAppearanceComponent.Gender;
                    if (fingerprintComponent != null)
                    {
                        record.Fingerprint = fingerprintComponent.Fingerprint;
                    }

                    if (dnaComponent != null)
                    {
                        record.DNA = dnaComponent.DNA;
                    }

                    _recordsSystem.Synchronize(recordKey);
                }

            }
        }
        else if(station != null && _idCardSystem.TryFindIdCard(args.EntityUid, out var idCard))
        {
            var jobPrototype = _prototypeManager.Index<JobPrototype>(JobPrisoner);

            var record = new GeneralStationRecord()
            {
                Name = Name(args.EntityUid),
                Age = humanoidAppearanceComponent.Age,
                JobTitle = jobPrototype.LocalizedName,
                JobIcon = jobPrototype.Icon,
                JobPrototype = JobPrisoner,
                Species = humanoidAppearanceComponent.Species,
                Gender = humanoidAppearanceComponent.Gender,
                DisplayPriority = jobPrototype.RealDisplayWeight,
                Fingerprint = fingerprintComponent?.Fingerprint,
                DNA = dnaComponent?.DNA
            };

            var key = _recordsSystem.AddRecordEntry(station.Value, record);
            _recordsSystem.SetIdKey(idCard, key);
            _recordsSystem.Synchronize(key);
        }
    }


    [ValidatePrototypeId<EntityPrototype>]
    private const string DefaultSuperPsiRule = "SuperPsiRule";

    private void OnMindAdded(Entity<AutoPsiComponent> ent, ref MindAddedMessage args)
    {
        RemCompDeferred<AutoPsiComponent>(ent);
        _antag.ForceMakeAntag<SuperPsiRuleComponent>(args.Mind.Comp.Session, DefaultSuperPsiRule);
    }
}
