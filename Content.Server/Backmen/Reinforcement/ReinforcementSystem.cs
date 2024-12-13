using System.Linq;
using Content.Server.Administration.Logs;
using Content.Server.Administration.Managers;
using Content.Server.Backmen.Reinforcement.Components;
using Content.Server.Chat.Managers;
using Content.Server.GameTicking;
using Content.Server.Ghost.Roles.Components;
using Content.Server.Ghost.Roles.Raffles;
using Content.Server.Mind;
using Content.Server.Players.JobWhitelist;
using Content.Server.Players.PlayTimeTracking;
using Content.Server.Popups;
using Content.Server.Roles;
using Content.Server.Station.Systems;
using Content.Shared.Access.Systems;
using Content.Shared.Backmen.Cryostorage;
using Content.Shared.Backmen.Reinforcement;
using Content.Shared.Backmen.Reinforcement.Components;
using Content.Shared.Database;
using Content.Shared.GameTicking;
using Content.Shared.Humanoid;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Content.Shared.Roles.Jobs;
using Content.Shared.UserInterface;
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
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IBanManager _banManager = default!;
    [Dependency] private readonly PlayTimeTrackingSystem _playTimeTrackings = default!;
    [Dependency] private readonly SharedHumanoidAppearanceSystem _appearance = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly SharedRoleSystem _roles = default!;
    [Dependency] private readonly SharedJobSystem _jobs = default!;
    [Dependency] private readonly IAdminLogManager _adminLogger = default!;
    [Dependency] private readonly StationJobsSystem _stationJobs = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly GameTicker _ticker = default!;
    [Dependency] private readonly JobWhitelistManager _jobWhitelistManager = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ReinforcementSpawnerComponent, TakeGhostRoleEvent>(OnTakeoverTakeRole);
        SubscribeLocalEvent<ReinforcementSpawnPlayer>(OnSpawnPlayer);
        SubscribeLocalEvent<ReinforcementConsoleComponent,ActivatableUIOpenAttemptEvent>(OnTryOpenUi);
        SubscribeLocalEvent<ReinforcementMemberComponent, MovedToStorageEvent>(OnMoveToSSD);
        SubscribeLocalEvent<ReinforcementMindComponent, GetBriefingEvent>(OnGetBrief);

        Subs.BuiEvents<ReinforcementConsoleComponent>(ReinforcementConsoleKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(UpdateUserInterface);
            subs.Event<ChangeReinforcementMsg>(OnKeySelected);
            subs.Event<BriefReinforcementUpdate>(OnBriefUpdate);
            subs.Event<CallReinforcementStart>(OnStartCall);
        });
    }

    private void OnGetBrief(Entity<ReinforcementMindComponent> ent, ref GetBriefingEvent args)
    {
        args.Append("Вы вызваны как подкрепление, брифинг: ");
        if (TerminatingOrDeleted(ent.Comp.Linked))
        {
            args.Append("- нет связи с сервером -");
            return;
        }
        args.Append(ent.Comp.Linked.Comp.Brief);
    }

    private void OnMoveToSSD(Entity<ReinforcementMemberComponent> ent, ref MovedToStorageEvent args)
    {
        if (TerminatingOrDeleted(ent.Comp.Linked))
        {
            args.Storage.Comp.StoredPlayers.Remove(ent);
            Dirty(args.Storage);
            QueueDel(ent);
            return;
        }
        var members = ent.Comp.Linked.Comp.Members;
        foreach (var member in ent.Comp.Linked.Comp.Members.ToArray())
        {
            if (member.Owner != ent.Owner)
                continue;
            ent.Comp.Linked.Comp.Members.Remove(member);
            break;
        }
        var cc = members.Count;
        if(cc == 0)
            Clear(ent.Comp.Linked);

        args.Storage.Comp.StoredPlayers.Remove(ent);
        QueueDel(ent);
        Dirty(args.Storage);
    }

    private void OnTryOpenUi(Entity<ReinforcementConsoleComponent> ent, ref ActivatableUIOpenAttemptEvent args)
    {
        if (!_access.IsAllowed(args.User, ent))
        {
            _popup.PopupCursor(Loc.GetString("reinforcement-insufficient-access"), args.User, PopupType.Medium);
            args.Cancel();
        }
    }

    public void Clear(Entity<ReinforcementConsoleComponent> ent)
    {
        ent.Comp.Brief = "";
        ent.Comp.CalledBy = EntityUid.Invalid;
        ent.Comp.Members.Clear();
        ent.Comp.IsActive = false;
        UpdateUserInterface(ent);
    }

    private void OnSpawnPlayer(ReinforcementSpawnPlayer args)
    {
        var ent = args.Source;
        var station = _station.GetOwningStation(ent.Comp.Linked);
        if (station == null)
            return;

        var character = _ticker.GetPlayerProfile(args.Player).Clone();
        character.Name = HumanoidCharacterProfile.GetName(character.Species, character.Gender);

        //var character = HumanoidCharacterProfile.RandomWithSpecies();
        var newMind = _mind.CreateMind(args.Player.UserId, character.Name);
        _mind.SetUserId(newMind, args.Player.UserId);

        var jobPrototype = _prototype.Index(args.Proto.Job);
        _roles.MindAddJobRole(newMind, silent: false, jobPrototype:args.Proto.Job);
        EnsureComp<ReinforcementMindComponent>(newMind).Linked = ent.Comp.Linked;
        var jobName = _jobs.MindTryGetJobName(newMind);

        _playTimeTrackings.PlayerRolesChanged(args.Player);

        //var mob = Spawn(proto.Spawn);
        var spawnEv = new PlayerSpawningEvent(args.Proto.Job, character, station);
        RaiseLocalEvent(spawnEv);

        var mob = spawnEv.SpawnResult ?? Spawn("MobHuman", Transform(ent).Coordinates);

        EnsureComp<ReinforcementMemberComponent>(mob).Linked = ent.Comp.Linked;
        _appearance.LoadProfile(mob, character);

        _mind.TransferTo(newMind, mob);

        _stationJobs.TryAssignJob(station.Value, jobPrototype, args.Player.UserId);
        _adminLogger.Add(LogType.LateJoin, LogImpact.Medium, $"Player {args.Player.Name} late joined as {character.Name:characterName} on station {Name(station.Value):stationName} with {ToPrettyString(mob):entity} as a {jobName:jobName}.");

        if (TryComp(station, out MetaDataComponent? metaData))
        {
            _chatManager.DispatchServerMessage(args.Player,
                Loc.GetString("job-greet-station-name", ("stationName", metaData.EntityName)));
        }

        var ev = new PlayerSpawnCompleteEvent(mob, args.Player, args.Proto.Job, true, true, 0, station.Value, character);
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
            ent.Comp.Used = false;
            return;
        }

        var proto = ent.Comp.Linked.Comp.GetById(row.Id, _prototype);
        if (proto == null)
        {
            ent.Comp.Used = false;
            return;
        }

        if (_banManager.GetJobBans(args.Player.UserId)?.Contains(proto.Job) ?? false)
        {
            ent.Comp.Used = false;
            SendChatMsg(args.Player, Loc.GetString("role-ban"));
            return;
        }

        if (!_playTimeTrackings.IsAllowed(args.Player,proto.Job))
        {
            ent.Comp.Used = false;
            SendChatMsg(args.Player, Loc.GetString("role-timer-locked"));
            return;
        }

        if (!_jobWhitelistManager.IsAllowed(args.Player, proto.Job))
        {
            ent.Comp.Used = false;
            SendChatMsg(args.Player, Loc.GetString("role-not-whitelisted"));
            return;
        }

        QueueLocalEvent(new ReinforcementSpawnPlayer(args.Player, ent, row, proto));
        args.TookRole = true;
    }

    [ValidatePrototypeId<EntityPrototype>]
    private const string Spawner = "ReinforcementSpawner";

    private void OnStartCall(Entity<ReinforcementConsoleComponent> ent, ref CallReinforcementStart args)
    {
        if (!_access.IsAllowed(args.Actor, ent))
        {
            _popup.PopupCursor(Loc.GetString("reinforcement-insufficient-access"), args.Actor, PopupType.Medium);
            return;
        }

        if (ent.Comp.IsActive)
        {
            return;
        }

        if (ent.Comp.Members.Count > ent.Comp.MaxMembers)
        {
            _popup.PopupEntity(Loc.GetString("reinforcement-error-list-1", ("num",ent.Comp.MaxMembers)), ent, args.Actor, PopupType.LargeCaution);
            return;
        }
        if (ent.Comp.Members.Count < ent.Comp.MinMembers)
        {
            _popup.PopupEntity(Loc.GetString("reinforcement-error-list-2", ("num",ent.Comp.MinMembers)), ent, args.Actor, PopupType.LargeCaution);
            return;
        }
        if (ent.Comp.Members.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("reinforcement-error-list"), ent, args.Actor, PopupType.LargeCaution);
            return;
        }

        if (ent.Comp.Brief.Length == 0)
        {
            _popup.PopupEntity(Loc.GetString("reinforcement-error-brief"), ent, args.Actor, PopupType.LargeCaution);
            return;
        }

        ent.Comp.IsActive = true;
        ent.Comp.CalledBy = args.Actor;

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
            ghost.RaffleConfig = new GhostRoleRaffleConfig
            {
                Settings = "default"
            };

            ghost.RoleName = Loc.GetString("reinforcement-ghostrole-name", ("name", proto.Name));
            ghost.RoleDescription = Loc.GetString("reinforcement-ghostrole-desc", ("job", Loc.GetString(job.Name)));
            ghost.RoleRules = Loc.GetString("reinforcement-ghostrole-rule", ("brief", ent.Comp.Brief));

            if (job.Requirements != null)
            {
                ghost.Requirements = new HashSet<JobRequirement>(job.Requirements);
            }

            ghost.JobProto = job.ID;

            //ghost.WhitelistRequired = job.Whitelisted;
        }

        UpdateUserInterface(ent);
    }

    private void OnBriefUpdate(Entity<ReinforcementConsoleComponent> ent, ref BriefReinforcementUpdate args)
    {
        if (ent.Comp.IsActive)
        {
            return;
        }

        var brief = (args.Brief ?? string.Empty);
        ent.Comp.Brief = brief[..Math.Min(brief.Length,ent.Comp.MaxStringLength)];
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
                MobState state;
                if (HasComp<ReinforcementSpawnerComponent>(member.Owner))
                {
                    state = MobState.Invalid;
                }
                else if (TryComp<MobStateComponent>(member.Owner, out var mobState))
                {
                    state = mobState.CurrentState;
                }
                else
                {
                    state = MobState.Dead;
                }
                msg.Members.Add((member.Id, GetNetEntity(member.Owner), member.Name, state));
            }
            else
            {
                msg.Members.Add((member.Id, NetEntity.Invalid, member.Name, MobState.Invalid));
            }
        }

        _ui.SetUiState(uid.Owner, ReinforcementConsoleKey.Key, msg);
    }

    private void SendChatMsg(ICommonSession sess, string message)
    {
        _popup.PopupCursor(message, sess, PopupType.LargeCaution);
        _chatManager.ChatMessageToOne(Shared.Chat.ChatChannel.Server,
            message,
            Loc.GetString("chat-manager-server-wrap-message", ("message", message)),
            default,
            false,
            sess.Channel,
            Color.Red);
    }
}
