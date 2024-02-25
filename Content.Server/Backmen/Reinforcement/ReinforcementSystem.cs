using System.Linq;
using Content.Server.Administration.Logs;
using Content.Server.Administration.Managers;
using Content.Server.Backmen.Reinforcement.Components;
using Content.Server.Chat.Managers;
using Content.Server.GameTicking;
using Content.Server.Ghost.Roles.Components;
using Content.Server.Mind;
using Content.Server.Players.PlayTimeTracking;
using Content.Server.Popups;
using Content.Server.Station.Systems;
using Content.Shared.Access.Systems;
using Content.Shared.Backmen.Reinforcement;
using Content.Shared.Backmen.Reinforcement.Components;
using Content.Shared.Database;
using Content.Shared.Humanoid;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Content.Shared.Roles.Jobs;
using Robust.Server.GameObjects;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server.Backmen.Reinforcement;


public class ReinforcementSpawnPlayer : EntityEventArgs
{
    public ICommonSession Player { get; }
    public Entity<ReinforcementSpawnerComponent> Source { get; }
    public ReinforcementRowRecord Row { get; }
    public ReinforcementPrototype Proto { get; }

    public ReinforcementSpawnPlayer(
        ICommonSession player,
        Entity<ReinforcementSpawnerComponent> source,
        ReinforcementRowRecord row,
        ReinforcementPrototype proto)
    {
        Player = player;
        Source = source;
        Row = row;
        Proto = proto;
    }
}

public sealed class ReinforcementSystem : SharedReinforcementSystem
{
    [Dependency] private readonly AccessReaderSystem _access = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedIdCardSystem _idCard = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IBanManager _banManager = default!;
    [Dependency] private readonly PlayTimeTrackingSystem _playTimeTrackings = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly SharedHumanoidAppearanceSystem _appearance = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly SharedRoleSystem _roles = default!;
    [Dependency] private readonly SharedJobSystem _jobs = default!;
    [Dependency] private readonly IAdminLogManager _adminLogger = default!;
    [Dependency] private readonly StationJobsSystem _stationJobs = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly StationSpawningSystem _stationSpawning = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ReinforcementSpawnerComponent, TakeGhostRoleEvent>(OnTakeoverTakeRole);
        SubscribeLocalEvent<ReinforcementSpawnPlayer>(OnSpawnPlayer);

        Subs.BuiEvents<ReinforcementConsoleComponent>(ReinforcementConsoleKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(UpdateUserInterface);
            subs.Event<ChangeReinforcementMsg>(OnKeySelected);
            subs.Event<BriefReinforcementUpdate>(OnBriefUpdate);
            subs.Event<CallReinforcementStart>(OnStartCall);
        });
    }

    private void OnSpawnPlayer(ReinforcementSpawnPlayer args)
    {
        var ent = args.Source;
        var station = _station.GetOwningStation(ent.Comp.Linked);
        if (station == null)
            return;

        var character = HumanoidCharacterProfile.Random();
        var newMind = _mind.CreateMind(args.Player.UserId, character.Name);
        _mind.SetUserId(newMind, args.Player.UserId);

        var jobPrototype = _prototype.Index<JobPrototype>(args.Proto.Job);
        var job = new JobComponent { Prototype = args.Proto.Job };
        _roles.MindAddRole(newMind, job, silent: false);
        var jobName = _jobs.MindTryGetJobName(newMind);

        _playTimeTrackings.PlayerRolesChanged(args.Player);

        //var mob = Spawn(proto.Spawn);
        var spawnEv = new PlayerSpawningEvent(job, character, station);
        RaiseLocalEvent(spawnEv);

        var mob = spawnEv.SpawnResult ?? Spawn(args.Proto.Spawn, Transform(ent).Coordinates);

        _appearance.LoadProfile(mob, character);

        _mind.TransferTo(newMind, mob);

        _stationJobs.TryAssignJob(station.Value, jobPrototype, args.Player.UserId);
        _adminLogger.Add(LogType.LateJoin, LogImpact.Medium, $"Player {args.Player.Name} late joined as {character.Name:characterName} on station {Name(station.Value):stationName} with {ToPrettyString(mob):entity} as a {jobName:jobName}.");

        if (TryComp(station, out MetaDataComponent? metaData))
        {
            _chatManager.DispatchServerMessage(args.Player,
                Loc.GetString("job-greet-station-name", ("stationName", metaData.EntityName)));
        }

        var ev = new PlayerSpawnCompleteEvent(mob, args.Player, args.Proto.Job, true, 0, station.Value, character);
        RaiseLocalEvent(ev);

        EnsureComp<GhostRoleComponent>(ent).Taken = true;
        args.Row.Name = Name(mob);
        args.Row.Owner = mob;

        UpdateUserInterface(ent.Comp.Linked);
        QueueDel(ent);
    }

    private void OnTakeoverTakeRole(Entity<ReinforcementSpawnerComponent> ent, ref TakeGhostRoleEvent args)
    {
        if (args.TookRole || ent.Comp.Used)
        {
            return;
        }
        ent.Comp.Used = true;

        var row = ent.Comp.Linked.Comp.Members.FirstOrDefault(x => x.Owner == ent.Owner);
        if (row == null)
        {
            return;
        }

        var proto = ent.Comp.Linked.Comp.GetById(row.Id, _prototype);
        if (proto == null)
        {
            return;
        }

        if (_banManager.GetRoleBans(args.Player.UserId)?.Contains(proto.Job) ?? false)
        {
            _popup.PopupCursor(Loc.GetString("role-ban"), args.Player, PopupType.LargeCaution);
            ent.Comp.Used = false;
            return;
        }

        if (!_playTimeTrackings.IsAllowed(args.Player,proto.Job))
        {
            _popup.PopupCursor(Loc.GetString("role-timer-locked"), args.Player, PopupType.LargeCaution);
            ent.Comp.Used = false;
            return;
        }

        QueueLocalEvent(new ReinforcementSpawnPlayer(args.Player, ent, row, proto));
        args.TookRole = true;
    }

    [ValidatePrototypeId<EntityPrototype>]
    private const string Spawner = "ReinforcementSpawner";

    private void OnStartCall(Entity<ReinforcementConsoleComponent> ent, ref CallReinforcementStart args)
    {
        if (ent.Comp.IsActive)
        {
            return;
        }

        if (ent.Comp.Members.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("reinforcement-error-list"), ent, args.Session, PopupType.LargeCaution);
            return;
        }

        if (ent.Comp.Brief.Length == 0)
        {
            _popup.PopupEntity(Loc.GetString("reinforcement-error-brief"), ent, args.Session, PopupType.LargeCaution);
            return;
        }

        ent.Comp.IsActive = true;

        foreach (var member in ent.Comp.Members)
        {
            var proto = ent.Comp.GetById(member.Id, _prototype);
            if (proto == null)
                continue;

            member.Name = "Ожидание...";
            var marker = Spawn(Spawner, Transform(ent).Coordinates);
            member.Owner = marker;
            EnsureComp<ReinforcementSpawnerComponent>(marker).Linked = ent;

            var job = _prototype.Index(proto.Job);

            var ghost = EnsureComp<GhostRoleComponent>(marker);
            ghost.RoleName = Loc.GetString("reinforcement-ghostrole-name", ("name", proto.Name));
            ghost.RoleDescription = Loc.GetString("reinforcement-ghostrole-desc", ("job", job.Name));
            ghost.RoleRules = Loc.GetString("reinforcement-ghostrole-rule", ("brief", ent.Comp.Brief));

            if (job.Requirements != null)
            {
                ghost.Requirements = new HashSet<JobRequirement>(job.Requirements);
            }

            ghost.WhitelistRequired = job.WhitelistRequired;
        }

        UpdateUserInterface(ent);
    }

    private void OnBriefUpdate(Entity<ReinforcementConsoleComponent> ent, ref BriefReinforcementUpdate args)
    {
        if (ent.Comp.IsActive)
        {
            return;
        }

        ent.Comp.Brief = args.Brief ?? string.Empty;
        //UpdateUserInterface(ent);
    }

    private void OnKeySelected(Entity<ReinforcementConsoleComponent> ent, ref ChangeReinforcementMsg args)
    {
        if (ent.Comp.IsActive || args.Id == null)
        {
            return;
        }

        var id = args.Id.Value;

        var cur = ent.Comp.Members.Where(x => x.Id == id).ToArray();
        if (cur.Length == args.Count)
        {
            return; // no changes?
        }

        if (cur.Length > args.Count) // minus
        {
            foreach (var rowRecord in cur.Take((int) (cur.Length - args.Count)))
            {
                ent.Comp.Members.Remove(rowRecord);
            }
        }
        else //plus
        {
            for (var i = 0; i < (args.Count - cur.Length); i++)
            {
                ent.Comp.Members.Add(new ReinforcementRowRecord()
                {
                    Id = id
                });
            }
        }
        //UpdateUserInterface(ent);
    }

    private void UpdateUserInterface<T>(Entity<ReinforcementConsoleComponent> ent, ref T args)
    {
        UpdateUserInterface(ent);
    }

    private void UpdateUserInterface(Entity<ReinforcementConsoleComponent> uid)
    {
        var msg = new UpdateReinforcementUi();
        msg.Brief = uid.Comp.Brief;
        msg.IsActive = uid.Comp.IsActive;

        if (TryComp<MetaDataComponent>(uid.Comp.CalledBy, out var calledBy))
        {
            msg.CalledBy = calledBy.EntityName;
        }

        foreach (var member in uid.Comp.Members)
        {
            if (member.Owner.Valid)
            {
                var state = TryComp<MobStateComponent>(member.Owner, out var mobState)
                    ? mobState.CurrentState
                    : MobState.Dead;
                msg.Members.Add((member.Id, GetNetEntity(member.Owner), member.Name, state));
            }
            else
            {
                msg.Members.Add((member.Id, NetEntity.Invalid, member.Name, MobState.Invalid));
            }
        }

        _ui.TrySetUiState(uid, ReinforcementConsoleKey.Key, msg);
    }
}
