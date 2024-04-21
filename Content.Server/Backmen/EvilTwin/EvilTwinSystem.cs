﻿using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using Content.Server.Access.Systems;
using Content.Server.Administration.Logs;
using Content.Server.Backmen.Cloning;
using Content.Server.Backmen.Economy;
using Content.Server.Backmen.Fugitive;
using Content.Server.CartridgeLoader.Cartridges;
using Content.Server.DetailExaminable;
using Content.Server.Forensics;
using Content.Server.GameTicking;
using Content.Server.Humanoid;
using Content.Server.Prayer;
using Content.Server.Preferences.Managers;
using Content.Server.Shuttles.Components;
using Content.Server.Spawners.Components;
using Content.Server.Station.Systems;
using Content.Shared.CombatMode.Pacification;
using Content.Shared.Database;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Mobs;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Content.Shared.Tag;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Content.Server.Ghost.Roles.Events;
using Content.Server.Mind;
using Content.Server.Objectives;
using Content.Server.Objectives.Components;
using Content.Server.Objectives.Systems;
using Content.Server.PDA;
using Content.Server.Roles;
using Content.Server.Station.Components;
using Content.Shared.Access.Components;
using Content.Shared.CartridgeLoader;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Content.Shared.Inventory;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.NukeOps;
using Content.Shared.Objectives.Components;
using Content.Shared.PDA;
using Content.Shared.Players;
using Content.Shared.Roles.Jobs;
using Content.Shared.StatusIcon;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server.Backmen.EvilTwin;

public sealed class EvilTwinSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EvilTwinSpawnerComponent, PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<EvilTwinComponent, MindAddedMessage>(OnMindAdded);
        SubscribeLocalEvent<RoundEndTextAppendEvent>(OnRoundEnd);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnCleanup);
        SubscribeLocalEvent<EvilTwinSpawnerComponent, GhostRoleSpawnerUsedEvent>(OnGhostRoleSpawnerUsed);
        SubscribeLocalEvent<EvilTwinComponent, MobStateChangedEvent>(OnHandleComponentState);
        SubscribeLocalEvent<PickEvilTwinPersonComponent, ObjectiveAssignedEvent>(OnPersonAssigned);
        SubscribeLocalEvent<SpawnEvilTwinEvent>(OnSpawn);
    }

    private void OnSpawn(SpawnEvilTwinEvent ev)
    {
        var uid = ev.Entity.Owner;
        var component = ev.Entity.Comp;
        HumanoidCharacterProfile? pref = null;

        EntityUid? targetUid = null;

        if (component.TargetForce != EntityUid.Invalid)
        {
            if (IsEligibleHumanoid(component.TargetForce))
            {
                targetUid = component.TargetForce;
            }
        }
        else
        {
            TryGetEligibleHumanoid(out targetUid);
        }

        try
        {
            if (targetUid.HasValue)
            {
                var xform = Transform(uid);
                (var twinMob, pref) = SpawnEvilTwin(targetUid.Value, xform.Coordinates);
                if (twinMob != null)
                {
                    if (_mindSystem.TryGetMind(ev.Session, out var mindId, out var mind))
                    {
                        _mindSystem.TransferTo(mindId, null, true, false, mind);
                        RemComp<MindContainerComponent>(twinMob.Value);
                        Timer.Spawn(0, () =>
                        {
                            _mindSystem.TransferTo(mindId, twinMob, true, false, mind);
                        });

                        var station = _stationSystem.GetOwningStation(targetUid.Value) ?? _stationSystem.GetStations()
                            .FirstOrNull(HasComp<StationEventEligibleComponent>);
                        if (pref != null && station != null &&
                            _mindSystem.TryGetMind(targetUid.Value, out var targetMindId, out var targetMind)
                            && _roles.MindHasRole<JobComponent>(targetMindId))
                        {
                            /*if (TryComp<BankMemoryComponent>(targetMindId, out var mindBank) && TryComp<BankAccountComponent>(mindBank.BankAccount, out var bankAccountComponent))
                            {
                                _economySystem.AddPlayerBank(twinMob.Value, bankAccountComponent);
                                if (TryComp<BankMemoryComponent>(mindId, out var twinBank))
                                {
                                    twinBank.BankAccount = mindBank.BankAccount;
                                }
                            }*/

                            var currentJob = Comp<JobComponent>(targetMindId);

                            var targetSession = targetMind?.Session;
                            var targetUserId = targetMind?.UserId ?? targetMind?.OriginalOwnerUserId;
                            if (targetUserId == null)
                            {
                                targetSession = ev.Session;
                            }
                            else if (targetSession == null)
                            {
                                targetSession = _playerManager.GetSessionById(targetUserId.Value);
                            }

                            RaiseLocalEvent(new PlayerSpawnCompleteEvent(twinMob.Value,
                                targetSession,
                                currentJob?.Prototype, false,
                                0, station.Value, pref));

                            if (!_roles.MindHasRole<JobComponent>(mindId))
                            {
                                _roles.MindAddRole(mindId, new JobComponent() { Prototype = currentJob?.Prototype });
                            }

                            if (_inventory.TryGetSlotEntity(targetUid.Value, "id", out var targetPda) &&
                                _inventory.TryGetSlotEntity(twinMob.Value, "id", out var twinPda) &&
                                TryComp<CartridgeLoaderComponent>(targetPda, out var targetPdaComp) &&
                                TryComp<CartridgeLoaderComponent>(twinPda, out var twinPdaComp))
                            {
                                var twinProgram =
                                    twinPdaComp.BackgroundPrograms.FirstOrDefault(
                                        HasComp<NotekeeperCartridgeComponent>);
                                var targetProgram =
                                    targetPdaComp.BackgroundPrograms.FirstOrDefault(
                                        HasComp<NotekeeperCartridgeComponent>);
                                if (twinProgram.Valid &&
                                    targetProgram.Valid &&
                                    TryComp<NotekeeperCartridgeComponent>(targetProgram, out var targetNotesComp) &&
                                    TryComp<NotekeeperCartridgeComponent>(twinProgram, out var twinNotesComp))
                                {
                                    foreach (var note in targetNotesComp.Notes)
                                    {
                                        twinNotesComp.Notes.Add(note);
                                    }
                                }
                            }
                        }

                        _allEvilTwins.Add((twinMob.Value, mind));
                        _adminLogger.Add(LogType.Action, LogImpact.Extreme,
                            $"{_entityManager.ToPrettyString(twinMob.Value)} take EvilTwin with target {_entityManager.ToPrettyString(targetUid.Value)}");
                    }
                }
            }
            else
            {
                _adminLogger.Add(LogType.Action, LogImpact.Extreme,
                    $"{_entityManager.ToPrettyString(uid)} take EvilTwin with no target (delete)");
                _prayerSystem.SendSubtleMessage(ev.Session, ev.Session, Loc.GetString("evil-twin-error-message"),
                    Loc.GetString("prayer-popup-subtle-default"));
            }
        }
        finally
        {
            QueueDel(uid);
        }
    }

    private List<(EntityUid Id, MindComponent Mind)> _allEvilTwins = new();

    private void OnCleanup(RoundRestartCleanupEvent ev)
    {
        _allEvilTwins.Clear();
    }

    private void OnPersonAssigned(EntityUid uid, PickEvilTwinPersonComponent component, ref ObjectiveAssignedEvent args)
    {
        // invalid objective prototype
        if (!TryComp<TargetObjectiveComponent>(uid, out var target))
        {
            args.Cancelled = true;
            return;
        }

        // target already assigned
        if (target.Target != null)
            return;

        if (!TryComp<EvilTwinRoleComponent>(args.MindId, out var rule) ||
            rule.Target == null || !rule.Target.Value.IsValid() || TerminatingOrDeleted(rule.Target.Value))
        {
            args.Cancelled = true;
            return;
        }

        if (!TryComp<MindContainerComponent>(rule.Target, out var targetMind) || !targetMind.HasMind)
        {
            args.Cancelled = true;
            return;
        }

        _target.SetTarget(uid, targetMind.Mind!.Value, target);
    }

    private void OnGhostRoleSpawnerUsed(EntityUid uid, EvilTwinSpawnerComponent component,
        GhostRoleSpawnerUsedEvent args)
    {
        if (TerminatingOrDeleted(args.Spawner) || EntityManager.IsQueuedForDeletion(args.Spawner))
        {
            return;
        }

        //forward
        if (TryComp<EvilTwinSpawnerComponent>(args.Spawner, out var comp))
        {
            component.TargetForce = comp.TargetForce;
        }

        QueueDel(args.Spawner);
    }

    private void OnHandleComponentState(EntityUid uid, EvilTwinComponent component, MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Dead && _mindSystem.TryGetMind(uid, out _, out var mindData))
        {
            mindData.PreventGhosting = false;
        }
    }

    public bool MakeTwin([NotNullWhen(true)] out EntityUid? twinSpawn, EntityUid? uid = null)
    {
        twinSpawn = null;

        EntityUid? station = null;

        if (uid.HasValue)
        {
            station = _stationSystem.GetOwningStation(uid.Value);
        }

        station ??= _stationSystem.GetStations().FirstOrNull(HasComp<StationEventEligibleComponent>);

        if (station == null || !TryComp<StationDataComponent>(station, out var stationDataComponent))
        {
            return false;
        }

        var spawnGrid = stationDataComponent.Grids.FirstOrNull(HasComp<BecomesStationComponent>);
        if (spawnGrid == null)
        {
            return false;
        }

        var latejoin = (from s in EntityQuery<SpawnPointComponent, TransformComponent>()
            where s.Item1.SpawnType == SpawnPointType.LateJoin && s.Item2.GridUid == spawnGrid
            select s.Item2.Coordinates).ToList();

        if (latejoin.Count == 0)
        {
            return false;
        }

        var coords = _random.Pick(latejoin);
        twinSpawn = Spawn(SpawnPointPrototype, coords);

        if (uid.HasValue)
        {
            EnsureComp<EvilTwinSpawnerComponent>(twinSpawn.Value).TargetForce = uid.Value;
        }

        return true;
    }

    private void OnPlayerAttached(Entity<EvilTwinSpawnerComponent> uid, ref PlayerAttachedEvent args)
    {
        QueueLocalEvent(new SpawnEvilTwinEvent(uid, args.Player));
    }

    private void OnMindAdded(EntityUid uid, EvilTwinComponent component, MindAddedMessage args)
    {
        if (!_mindSystem.TryGetMind(uid, out var mindId, out var mind))
        {
            return;
        }

        _roles.MindAddRole(mindId,
            new EvilTwinRoleComponent
            {
                PrototypeId = EvilTwinRole, TargetMindId = component.TwinMindId, TargetMind = component.TwinMind,
                Target = component.TwinEntity
            });

        _mindSystem.TryAddObjective(mindId, mind, KillObjective);
        _mindSystem.TryAddObjective(mindId, mind, EscapeObjective);

        mind.PreventGhosting = true;

        RemComp<PacifiedComponent>(uid);

        EnsureComp<PendingClockInComponent>(uid);

        _tagSystem.AddTag(uid, "CannotSuicide");
    }

    #region OnRoundEnd

    private void OnRoundEnd(RoundEndTextAppendEvent ev)
    {
        if (_allEvilTwins.Count < 1)
        {
            return;
        }

        var result = new StringBuilder();
        result.Append(Loc.GetString("evil-twin-round-end-result", ("evil-twin-count", _allEvilTwins.Count)));
        foreach (var (mindId, mind) in _allEvilTwins)
        {
            var name = mind.CharacterName;
            var username = mind.Session?.Name;
            var objectives = mind.AllObjectives.ToArray();
            if (objectives.Length == 0)
            {
                if (username != null)
                {
                    if (name == null)
                    {
                        result.Append("\n" + Loc.GetString("evil-twin-user-was-an-evil-twin", ("user", username)));
                    }
                    else
                    {
                        result.Append("\n" + Loc.GetString("evil-twin-user-was-an-evil-twin-named", ("user", username),
                            ("name", name)));
                    }
                }
                else if (name != null)
                {
                    result.Append("\n" + Loc.GetString("evil-twin-was-an-evil-twin-named", ("name", name)));
                }

                continue;
            }

            if (username != null)
            {
                if (name == null)
                {
                    result.Append("\n" + Loc.GetString("evil-twin-user-was-an-evil-twin-with-objectives",
                        ("user", username)));
                }
                else
                {
                    result.Append("\n" + Loc.GetString("evil-twin-user-was-an-evil-twin-with-objectives-named",
                        ("user", username), ("name", name)));
                }
            }
            else if (name != null)
            {
                result.Append("\n" + Loc.GetString("evil-twin-was-an-evil-twin-with-objectives-named", ("name", name)));
            }

            foreach (var objectiveGroup in objectives.GroupBy(o => Comp<ObjectiveComponent>(o).Issuer))
            {
                if (objectiveGroup.Key == "Космический банк")
                {
                    continue;
                }

                foreach (var objective in objectiveGroup)
                {
                    var info = _objectivesSystem.GetInfo(objective, mindId);
                    if (info == null)
                        continue;

                    var objectiveTitle = info.Value.Title;
                    var progress = info.Value.Progress;

                    if (progress > 0.99f)
                    {
                        result.Append("\n- " + Loc.GetString(
                            "objectives-condition-success",
                            ("condition", objectiveTitle),
                            ("markupColor", "green")
                        ));
                    }
                    else
                    {
                        result.Append("\n- " + Loc.GetString(
                            "objectives-condition-fail",
                            ("condition", objectiveTitle),
                            ("progress", (int) (progress * 100)),
                            ("markupColor", "red")
                        ));
                    }
                }
            }
        }

        ev.AddLine(result.ToString());
    }

    #endregion

    private bool IsEligibleHumanoid(EntityUid? uid)
    {
        if (!uid.HasValue || !uid.Value.IsValid())
        {
            return false;
        }

        return !(HasComp<MetempsychosisKarmaComponent>(uid) ||
                 HasComp<FugitiveComponent>(uid) ||
                 HasComp<EvilTwinComponent>(uid) ||
                 HasComp<NukeOperativeComponent>(uid));
    }

    private bool TryGetEligibleHumanoid([NotNullWhen(true)] out EntityUid? uid)
    {
        var targets = new List<EntityUid>();
        {
            var query = AllEntityQuery<ActorComponent, MindContainerComponent, HumanoidAppearanceComponent>();
            while (query.MoveNext(out var entityUid, out var actor, out var mindContainer, out _))
            {
                if (!IsEligibleHumanoid(entityUid))
                    continue;

                if (!mindContainer.HasMind || mindContainer.Mind == null ||
                    TerminatingOrDeleted(mindContainer.Mind.Value))
                {
                    continue;
                }

                if (!_roles.MindHasRole<JobComponent>(mindContainer.Mind.Value))
                {
                    continue;
                }

                targets.Add(entityUid);
            }
        }

        uid = null;

        if (targets.Count == 0)
        {
            return false;
        }

        uid = _random.Pick(targets);

        return true;
    }

    private (EntityUid?, HumanoidCharacterProfile? pref) SpawnEvilTwin(EntityUid target, EntityCoordinates coords)
    {
        if (!_mindSystem.TryGetMind(target, out var mindId, out var mind) ||
            !HasComp<HumanoidAppearanceComponent>(target))
        {
            return (null, null);
        }

        var targetSession = mind.UserId ?? mind.OriginalOwnerUserId;

        if (targetSession == null)
        {
            return (null, null);
        }

        var pref = (HumanoidCharacterProfile) _prefs.GetPreferences(targetSession.Value).SelectedCharacter;
        if (!_prototype.TryIndex<SpeciesPrototype>(pref.Species, out var species))
        {
            return (null, null);
        }

        var twinUid = Spawn(species.Prototype, coords);
        _humanoid.LoadProfile(twinUid, pref);
        _metaSystem.SetEntityName(twinUid, MetaData(target).EntityName);
        if (TryComp<DetailExaminableComponent>(target, out var detail))
        {
            EnsureComp<DetailExaminableComponent>(twinUid).Content = detail.Content;
        }

        _humanoidSystem.LoadProfile(twinUid, pref);

        if (pref.FlavorText != "" && _configurationManager.GetCVar(CCVars.FlavorText))
        {
            EnsureComp<DetailExaminableComponent>(twinUid).Content = pref.FlavorText;
        }

        if (TryComp<FingerprintComponent>(target, out var fingerprintComponent))
        {
            EnsureComp<FingerprintComponent>(twinUid).Fingerprint = fingerprintComponent.Fingerprint;
        }

        if (TryComp<DnaComponent>(target, out var dnaComponent))
        {
            EnsureComp<DnaComponent>(twinUid).DNA = dnaComponent.DNA;
        }


        if (TryComp<JobComponent>(mindId, out var jobComponent) && jobComponent.Prototype != null &&
            _prototype.TryIndex<JobPrototype>(jobComponent.Prototype, out var twinTargetMindJob))
        {
            if (_prototype.TryIndex<StartingGearPrototype>(twinTargetMindJob.StartingGear!, out var gear))
            {
                _stationSpawning.EquipStartingGear(twinUid, gear);
                EquipIdCard(twinUid, pref.Name, twinTargetMindJob,
                    _stationSystem.GetOwningStation(target));
            }

            foreach (var special in twinTargetMindJob.Special)
            {
                special.AfterEquip(twinUid);
            }
        }

        var twin = EnsureComp<EvilTwinComponent>(twinUid);
        twin.TwinMindId = mindId;
        twin.TwinMind = mind;
        twin.TwinEntity = target;

        return (twinUid, pref);
    }

    /// <summary>
    /// Equips an ID card and PDA onto the given entity.
    /// </summary>
    /// <param name="entity">Entity to load out.</param>
    /// <param name="characterName">Character name to use for the ID.</param>
    /// <param name="jobPrototype">Job prototype to use for the PDA and ID.</param>
    /// <param name="station">The station this player is being spawned on.</param>
    public void EquipIdCard(EntityUid entity, string characterName, JobPrototype jobPrototype, EntityUid? station)
    {
        if (!_inventorySystem.TryGetSlotEntity(entity, "id", out var idUid))
            return;

        var cardId = idUid.Value;
        if (TryComp<PdaComponent>(idUid, out var pdaComponent) && pdaComponent.ContainedId != null)
            cardId = pdaComponent.ContainedId.Value;

        if (!TryComp<IdCardComponent>(cardId, out var card))
            return;

        _cardSystem.TryChangeFullName(cardId, characterName, card);
        _cardSystem.TryChangeJobTitle(cardId, jobPrototype.LocalizedName, card);

        if (_prototype.TryIndex<StatusIconPrototype>(jobPrototype.Icon, out var jobIcon))
        {
            _cardSystem.TryChangeJobIcon(cardId, jobIcon, card);
        }

        var extendedAccess = false;
        if (station != null)
        {
            var data = Comp<StationJobsComponent>(station.Value);
            extendedAccess = data.ExtendedAccess;
        }

        _accessSystem.SetAccessToJob(cardId, jobPrototype, extendedAccess);

        if (pdaComponent != null)
            _pdaSystem.SetOwner(idUid.Value, pdaComponent, characterName);
    }

    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IServerPreferencesManager _prefs = default!;
    [Dependency] private readonly HumanoidAppearanceSystem _humanoid = default!;
    [Dependency] private readonly StationSpawningSystem _stationSpawning = default!;
    [Dependency] private readonly StationSystem _stationSystem = default!;
    [Dependency] private readonly PrayerSystem _prayerSystem = default!;
    [Dependency] private readonly IAdminLogManager _adminLogger = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly TagSystem _tagSystem = default!;
    [Dependency] private readonly MindSystem _mindSystem = default!;
    [Dependency] private readonly MetaDataSystem _metaSystem = default!;
    [Dependency] private readonly RoleSystem _roles = default!;
    [Dependency] private readonly IConfigurationManager _configurationManager = default!;
    [Dependency] private readonly HumanoidAppearanceSystem _humanoidSystem = default!;
    [Dependency] private readonly ObjectivesSystem _objectivesSystem = default!;
    [Dependency] private readonly TargetObjectiveSystem _target = default!;
    [Dependency] private readonly InventorySystem _inventorySystem = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IdCardSystem _cardSystem = default!;
    [Dependency] private readonly AccessSystem _accessSystem = default!;
    [Dependency] private readonly PdaSystem _pdaSystem = default!;

    [ValidatePrototypeId<AntagPrototype>] private const string EvilTwinRole = "EvilTwin";

    [ValidatePrototypeId<EntityPrototype>] private const string KillObjective = "KillObjectiveEvilTwin";

    [ValidatePrototypeId<EntityPrototype>] private const string EscapeObjective = "EscapeShuttleObjectiveEvilTwin";

    [ValidatePrototypeId<EntityPrototype>] private const string SpawnPointPrototype = "SpawnPointEvilTwin";
}

public sealed class SpawnEvilTwinEvent : EntityEventArgs
{
    public Entity<EvilTwinSpawnerComponent> Entity;
    public ICommonSession Session;

    public SpawnEvilTwinEvent(Entity<EvilTwinSpawnerComponent> entity, ICommonSession session)
    {
        Entity = entity;
        Session = session;
    }
}
