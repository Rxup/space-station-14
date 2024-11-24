using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Content.Server.Administration.Logs;
using Content.Server.Administration.Managers;
using Content.Server.Backmen.Administration.Bwoink.Gpt.Models;
using Content.Server.GameTicking;
using Content.Server.Mind;
using Content.Server.Roles;
using Content.Server.Station.Components;
using Content.Shared.Administration;
using Content.Shared.CCVar;
using Content.Shared.Guidebook;
using Content.Shared.Humanoid;
using Content.Shared.Mind;
using Content.Shared.Mobs.Systems;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.ContentPack;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Server.Backmen.Administration.Bwoink.Gpt;

public sealed partial class GptCommands : EntitySystem
{
    [Dependency] private readonly GptAhelpSystem _gptAhelpSystem = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly MindSystem _mindSystem = default!;
    [Dependency] private readonly MobStateSystem _mobStateSystem = default!;
    [Dependency] private readonly IAdminManager _adminManager = default!;
    [Dependency] private readonly RoleSystem _roleSystem = default!;
    [Dependency] private readonly IAdminLogManager _adminLogger = default!;
    [Dependency] private readonly IConfigurationManager _configuration = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IResourceManager _resourceManager = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EventGptFunctionCall>(OnFunctionCall);

        _gptAhelpSystem.AddFunction(new
        {
            name = "get_server_rules",
            description = "получить правила сервера",
            parameters = new
            {
                @type = "object",
                properties = new { }
            }
        });
        _gptAhelpSystem.AddFunction(new
        {
            name = "get_current_round",
            description = "текущий номер раунда в котором я играю",
            parameters = new
            {
                @type = "object",
                properties = new { }
            }
        });
        _gptAhelpSystem.AddFunction(new
        {
            name = "get_current_round_time",
            description = "текущие время раунда в котором я играю",
            parameters = new
            {
                @type = "object",
                properties = new { }
            }
        });
        _gptAhelpSystem.AddFunction(new
        {
            name = PlayerInfoFn,
            description = "мой рп персонаж",
            parameters = new
            {
                @type = "object",
                properties = new { }
            }
        });
        _gptAhelpSystem.AddFunction(new
        {
            name = "get_current_map",
            description = "текущая рп карта в которой я играю",
            parameters = new
            {
                @type = "object",
                properties = new { }
            }
        });
        _gptAhelpSystem.AddFunction(new
        {
            name = "get_current_admins",
            description = "получить список администоров сервера",
            parameters = new
            {
                @type = "object",
                properties = new { }
            }
        });
        _gptAhelpSystem.AddFunction(new
        {
            name = "get_current_logs",
            description = "получить логи действий текущего пользователя",
            parameters = new
            {
                @type = "object",
                properties = new { }
            }
        });
        _gptAhelpSystem.AddFunction(new
        {
            name = PlayerAntagInfoFn,
            description = "является запрашиваемый персонаж является ли предателем в рп в текущем раунде",
            parameters = new
            {
                @type = "object",
                properties = new
                {
                    character = new
                    {
                        @type = "string",
                        description =
                            "имя рп персанажа о котором спрашивают или мой текущий рп персонаж, например Ф И О или Ф И"
                    }
                },
                required = new[] { "character" }
            }
        });
    }

    private async Task FillLogs(EventGptFunctionCall ev)
    {
        var sess = _playerManager.GetPlayerData(ev.UserId);

        var admLogs = await _adminLogger.CurrentRoundLogs(new LogFilter
        {
            Limit = 100,
            Search = sess.UserName
        });

        ev.History.Messages.Add(
            new GptMessageFunction("get_current_logs", new { round = _gameTicker.RoundId, logs = admLogs.Select(x=>(x.Id,x.Date,x.Message)).ToArray() })
        );
        ev.Handled = true;
    }

    [ValidatePrototypeId<GuideEntryPrototype>]
    private const string DefaultRuleset = "DefaultRuleset";

    public GuideEntryPrototype GetCoreRuleEntry(string rule)
    {
        if (!_prototype.TryIndex<GuideEntryPrototype>(rule, out var guideEntryPrototype))
        {
            guideEntryPrototype = _prototype.Index<GuideEntryPrototype>(DefaultRuleset);
            return guideEntryPrototype;
        }

        return guideEntryPrototype!;
    }

    private void OnFunctionCall(EventGptFunctionCall ev)
    {
        if (ev.Handled)
        {
            return;
        }

        var fnName = ev.Msg.message.function_call?.name;
        switch (fnName)
        {
            case "get_server_rules":
            {
                var entry = GetCoreRuleEntry(_configuration.GetCVar(CCVars.RulesFile));
                using var file = _resourceManager.ContentFileReadText(entry.Text);
                ev.History.Messages.Add(new GptMessageFunction(fnName,
                    new { round = _gameTicker.RoundId, rules = file.ReadToEnd() }));
                ev.Handled = true;
            }
                break;
            case "get_current_logs":
                ev.HandlerTask = FillLogs(ev);
                break;
            case "get_current_round":
                ev.History.Messages.Add(new GptMessageFunction(fnName,
                    new { round = _gameTicker.RoundId, state = _gameTicker.RunLevel.ToString() }));
                ev.Handled = true;
                break;
            case "get_current_round_time":
                ev.History.Messages.Add(new GptMessageFunction(fnName, new { time = _gameTicker.RoundDuration() }));
                ev.Handled = true;
                break;
            case PlayerInfoFn:
                GetPlayerInfo(ev);
                ev.Handled = true;
                break;
            case "get_current_map":
            {
                var query =
                    EntityQueryEnumerator<StationJobsComponent, StationSpawningComponent, MetaDataComponent>();

                var stationNames = new List<string>();

                while (query.MoveNext(out _, out _, out var meta))
                {
                    stationNames.Add(meta.EntityName);
                }

                ev.History.Messages.Add(new GptMessageFunction(fnName, new { map = stationNames }));

                ev.Handled = true;
                break;
            }
            case "get_current_admins":
            {
                var admins = _adminManager.ActiveAdmins
                    .Where(p => _adminManager.GetAdminData(p)?.HasFlag(AdminFlags.Adminhelp) ?? false)
                    .Select(x => new { x.Data.UserName, title = _adminManager.GetAdminData(x)?.Title })
                    .ToArray();
                ev.History.Messages.Add(new GptMessageFunction(fnName, new { admin = admins }));
                ev.Handled = true;
                break;
            }
            case PlayerAntagInfoFn:
            {
                IsPlayerAntag(ev);
                ev.Handled = true;
                break;
            }
            default:
                return;
        }
    }

    private const string PlayerAntagInfoFn = "get_is_antag";

    private void IsPlayerAntag(EventGptFunctionCall ev)
    {
        var character = ev.Msg.message.function_call?.DecodeArgs<GetIsAntagArgs>()?.character;
        if (string.IsNullOrWhiteSpace(character))
        {
            ev.History.Messages.Add(new GptMessageFunction(PlayerAntagInfoFn));
            ev.Handled = true;
            return;
        }

        var antag = new List<string>();
        var query = EntityQueryEnumerator<MindComponent, MetaDataComponent>();
        while (query.MoveNext(out var mindId, out var mindComponent, out var meta))
        {
            if (
                meta.EntityName.Contains(character, StringComparison.InvariantCultureIgnoreCase) &&
                _roleSystem.MindIsAntagonist(mindId) && mindComponent.CharacterName != null)
            {
                antag.Add(mindComponent.CharacterName);
            }
        }

        ev.History.Messages.Add(new GptMessageFunction(PlayerAntagInfoFn,
            new { matchNames = antag, isAntag = antag.Count > 0 }));
    }


    private const string PlayerInfoFn = "get_current_char";

    private void GetPlayerInfo(EventGptFunctionCall ev)
    {
        if (!_playerManager.TryGetSessionById(ev.UserId, out var playerSession))
        {
            ev.History.Messages.Add(new GptMessageFunction(PlayerInfoFn)); // no info
            return;
        }

        var info = new Dictionary<string, object?>
        {
            ["name"] = playerSession.Data.UserName
        };

        var isHaveAttachedEntity = playerSession.AttachedEntity != null &&
                                   !TerminatingOrDeleted(playerSession.AttachedEntity.Value);
        var attachedEntity = playerSession.AttachedEntity ?? EntityUid.Invalid;

        info["ghost"] = isHaveAttachedEntity switch
        {
            // tell ghost with Player name
            false => true,
            true when _mindSystem.TryGetMind(ev.UserId, out var mindId, out var mind) =>
                _mobStateSystem.IsDead(attachedEntity) || mind.IsVisitingEntity,
            _ => info["ghost"]
        };

        if (isHaveAttachedEntity)
        {
            var md = MetaData(attachedEntity);
            info["name"] = md.EntityName;
            info["desc"] = md.EntityDescription;
        }

        if (isHaveAttachedEntity &&
            TryComp<HumanoidAppearanceComponent>(attachedEntity, out var humanoidAppearanceComponent))
        {
            info["age"] = humanoidAppearanceComponent.Age;
            info["gender"] = humanoidAppearanceComponent.Gender.ToString();
            info["skinColor"] = humanoidAppearanceComponent.SkinColor.ToHex();
            info["eyeColor"] = humanoidAppearanceComponent.EyeColor.ToHex();
            info["hairColor"] = humanoidAppearanceComponent.CachedHairColor?.ToHex();
        }

        ev.History.Messages.Add(new GptMessageFunction(PlayerInfoFn, info));
    }
}

// ReSharper disable once InconsistentNaming
public record GetIsAntagArgs(string character);
