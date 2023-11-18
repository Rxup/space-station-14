using System.Linq;
using System.Numerics;
using Content.Server.Administration.Commands;
using Content.Server.Cargo.Systems;
using Content.Server.Chat.Managers;
using Content.Server.GameTicking.Rules;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.Preferences.Managers;
using Content.Server.Spawners.Components;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Server.NPC.Systems;
using Content.Shared.CCVar;
using Content.Shared.Humanoid;
using Content.Shared.Mind;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Server.GameObjects;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Server.Maps;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;
using Content.Server.GameTicking;
using Content.Server.Backmen.GameTicking.Rules.Components;
using Content.Server.Antag;
using Content.Shared.Backmen.Magicians;

namespace Content.Server.Backmen.GameTicking.Rules;

public sealed class MagiciansRuleSystem : GameRuleSystem<MagiciansRuleComponent>
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly IServerPreferencesManager _prefs = default!;
    [Dependency] private readonly StationSpawningSystem _stationSpawningSystem = default!;
    [Dependency] private readonly MapLoaderSystem _map = default!;
    [Dependency] private readonly NamingSystem _namingSystem = default!;
    [Dependency] private readonly NpcFactionSystem _npcFaction = default!;
    [Dependency] private readonly AntagSelectionSystem _antagSelection = default!;
    [Dependency] private readonly SharedMindSystem _mindSystem = default!;
    [Dependency] private readonly SharedAudioSystem _audioSystem = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RulePlayerSpawningEvent>(OnPlayerSpawningEvent);
        SubscribeLocalEvent<RoundEndTextAppendEvent>(OnRoundEndTextEvent);
        SubscribeLocalEvent<RoundStartAttemptEvent>(OnStartAttempt);
    }


    private void OnRoundEndTextEvent(RoundEndTextAppendEvent ev)
    {
        var query = EntityQueryEnumerator<MagiciansRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out var mag, out var gameRule))
        {

            var magiciansquery = AllEntityQuery<MagiciansComponent, MobStateComponent>();
            var magicianslist = new List<EntityUid>();
            while (magiciansquery.MoveNext(out var maguid, out _, out _))
            {
                magicianslist.Add(maguid);
            }
            bool anyalive = false;

            foreach (var entity in magicianslist)
            {
                if (TryComp(entity, out MobStateComponent? state))
                {
                    if (state.CurrentState == MobState.Alive)
                    {
                        anyalive = true;
                    }
                }
            }

            if (!anyalive)
            {
                ev.AddLine(Loc.GetString("magicians-died"));
            } else
            {
                ev.AddLine(Loc.GetString("magicians-win"));
            }

            ev.AddLine("");
            ev.AddLine(Loc.GetString("magicians-list-start"));
            foreach (var magg in mag.Magicians)
            {
                if (TryComp(magg, out MindComponent? mind))
                {
                    ev.AddLine($"- {mind.CharacterName} ({mind.Session?.Name})");
                }
            }

        }
    }
    private void OnPlayerSpawningEvent(RulePlayerSpawningEvent ev)
    {
        var query = EntityQueryEnumerator<MagiciansRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out var mags, out var gameRule))
        {
            // Forgive me for copy-pasting nukies.
            if (!GameTicker.IsGameRuleAdded(uid, gameRule))
                return;

            mags.Magicians.Clear();

            // Between 1 and <max pirate count>: needs at least n players per op.
            var numOps = Math.Max(1,
                (int) Math.Min(
                    Math.Floor((double) ev.PlayerPool.Count / mags.MagPerPlayer),
                    mags.MaxMagicians));
            var ops = new ICommonSession[numOps];
            for (var i = 0; i < numOps; i++)
            {
                ops[i] = _random.PickAndTake(ev.PlayerPool);
            }

            var map = "/Maps/Shuttles/wizard.yml";
            var xformQuery = GetEntityQuery<TransformComponent>();

            var aabbs = EntityQuery<StationDataComponent>().SelectMany(x =>
                    x.Grids.Select(x =>
                        xformQuery.GetComponent(x).WorldMatrix.TransformBox(_mapManager.GetGridComp(x).LocalAABB)))
                .ToArray();

            var aabb = aabbs[0];

            for (var i = 1; i < aabbs.Length; i++)
            {
                aabb.Union(aabbs[i]);
            }

            // (Not commented?)
            var a = MathF.Max(aabb.Height / 2f, aabb.Width / 2f) * 2.5f;

            var gridId = _map.LoadGrid(GameTicker.DefaultMap, map, new MapLoadOptions
            {
                Offset = aabb.Center + new Vector2(a, a),
            });

            if (!gridId.HasValue)
            {
                Logger.ErrorS("magicians", $"Gridid was null when loading \"{map}\", aborting.");
                foreach (var session in ops)
                {
                    ev.PlayerPool.Add(session);
                }

                return;
            }

            mags.MagicianShip = gridId.Value;

            // TODO: Loot table or something
            var magicianGear = _prototypeManager.Index<StartingGearPrototype>("MagiciangGear");

            var spawns = new List<EntityCoordinates>();

            // Forgive me for hardcoding prototypes
            foreach (var (_, meta, xform) in
                     EntityQuery<SpawnPointComponent, MetaDataComponent, TransformComponent>(true))
            {
                if (meta.EntityPrototype?.ID != "SpawnPointCaptain" || xform.ParentUid != mags.MagicianShip)
                    continue;

                spawns.Add(xform.Coordinates);
            }

            if (spawns.Count == 0)
            {
                spawns.Add(Transform(mags.MagicianShip).Coordinates);
                Logger.WarningS("magicians", $"Fell back to default spawn for magicians!");
            }

            for (var i = 0; i < ops.Length; i++)
            {
                var sex = _random.Prob(0.5f) ? Sex.Male : Sex.Female;
                var gender = sex == Sex.Male ? Gender.Male : Gender.Female;

                var name = _namingSystem.GetName("Human", gender);

                var session = ops[i];
                var newMind = _mindSystem.CreateMind(session.UserId, name);
                _mindSystem.SetUserId(newMind, session.UserId);

                var mob = Spawn("MobHuman", _random.Pick(spawns));
                _metaData.SetEntityName(mob, name);

                EnsureComp<MagiciansComponent>(mob); // cka

                _mindSystem.TransferTo(newMind, mob);
                var profile = _prefs.GetPreferences(session.UserId).SelectedCharacter as HumanoidCharacterProfile;
                _stationSpawningSystem.EquipStartingGear(mob, magicianGear, profile);

                _npcFaction.RemoveFaction(mob, "NanoTrasen", false);
                _npcFaction.AddFaction(mob, "Syndicate");

                mags.Magicians.Add(newMind);

                // Notificate every player about a pirate antagonist role with sound
                _audioSystem.PlayGlobal(mags.MagsAlertSound, session);

                GameTicker.PlayerJoinGame(session);
            }
        }
    }

    private void OnStartAttempt(RoundStartAttemptEvent ev)
    {
        var query = EntityQueryEnumerator<MagiciansRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out var mags, out var gameRule))
        {
            if (!GameTicker.IsGameRuleActive(uid, gameRule))
                return;

            var minPlayers = mags.MinPlayers;
            if (!ev.Forced && ev.Players.Length < minPlayers)
            {
                _chatManager.SendAdminAnnouncement(Loc.GetString("nukeops-not-enough-ready-players",
                    ("readyPlayersCount", ev.Players.Length), ("minimumPlayers", minPlayers)));
                ev.Cancel();
                return;
            }

            if (ev.Players.Length == 0)
            {
                _chatManager.DispatchServerAnnouncement(Loc.GetString("nukeops-no-one-ready"));
                ev.Cancel();
            }
        }
    }
}
