using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Content.Server.Administration;
using Content.Server.Administration.Managers;
using Content.Shared.Administration;
using Content.Shared.GameTicking;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Console;
using Robust.Shared.Network;

namespace Content.Server.Backmen;

public enum GptUserDirection
{
    // ReSharper disable once InconsistentNaming
    user,

    // ReSharper disable once InconsistentNaming
    assistant
}

public record GptMessage(
    // ReSharper disable once InconsistentNaming
    GptUserDirection role,
    // ReSharper disable once InconsistentNaming
    string message);

#region ParamApi

public record GptApiMessage(string role, string content);

public record GptApiPacket(string model, GptApiMessage[] messages, float temperature = 0.7f)
{
    public bool stream = false;
}

#endregion

#region ResponseApi

public record GptResponseApiChoiceMsg(string content, string role);
public record GptResponseApiChoice(int index, GptResponseApiChoiceMsg message);
public record GptResponseApi(GptResponseApiChoice[] choices);

#endregion


public sealed class GptAhelpSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IConsoleHost _console = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IAdminManager _adminManager = default!;
    private readonly HttpClient _httpClient = new();

    private Dictionary<NetUserId, List<GptMessage>> _history = new();

    private bool _enabled = false;
    private string _apiUrl = "";
    private string _apiToken = "";
    private string _apiModel = "";


    private ISawmill _sawmill = default!;
    public override void Initialize()
    {
        base.Initialize();

        _sawmill = Logger.GetSawmill("gptchat");
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnCleanup);
        _cfg.OnValueChanged(Shared.Backmen.CCVar.CCVars.GptEnabled, GptEnabledCVarChanged, true);
        _cfg.OnValueChanged(Shared.Backmen.CCVar.CCVars.GptApiUrl, GptUrlCVarChanged, true);
        _cfg.OnValueChanged(Shared.Backmen.CCVar.CCVars.GptApiToken, GptTokenCVarChanged, true);
        _cfg.OnValueChanged(Shared.Backmen.CCVar.CCVars.GptModel, GptModelCVarChanged, true);

        _console.RegisterCommand("ahelp_gpt", GptCommand, GptCommandCompletion);
    }

    private CompletionResult GptCommandCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length != 1)
            return CompletionResult.Empty;

        return CompletionResult.FromHintOptions(CompletionHelper.SessionNames(), "Пользователь");
    }

    [AdminCommand(AdminFlags.Adminhelp)]
    private async void GptCommand(IConsoleShell shell, string argstr, string[] args)
    {
        if (!_enabled)
        {
            shell.WriteError("disabled!");
            return;
        }

        if (args.Length != 1)
        {
            shell.WriteError(Loc.GetString("shell-need-exactly-one-argument"));
            return;
        }

        if (!_playerManager.TryGetSessionByUsername(args[0], out var player))
        {
            shell.WriteError(Loc.GetString("parse-session-fail", ("username", args[0])));
            return;
        }

        var userId = player.UserId;

        if (!_history.ContainsKey(userId) || _history[userId].Count == 0)
        {
            shell.WriteError("Пользователь не писал сообщений!");
            return;
        }
        if (_history[userId].Last().role != GptUserDirection.user)
        {
            shell.WriteError("Пользователь не писал сообщений!");
            return;
        }

        var payload = new GptApiPacket(_apiModel, _history[userId].Select(x=>new GptApiMessage(x.role.ToString(), x.message)).ToArray(),0.8f);
        var request = await _httpClient.PostAsync($"{_apiUrl}chat/completions",
            new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));

        var response = await request.Content.ReadAsStringAsync();

        if (!request.IsSuccessStatusCode)
        {
            _sawmill.Debug(JsonSerializer.Serialize(payload));
            _sawmill.Debug(request.StatusCode.ToString());
            _sawmill.Debug(response);
            shell.WriteError($"Ошибка! GptChat: {request.StatusCode} - {response}");
            return;
        }

        var info = JsonSerializer.Deserialize<GptResponseApi>(response);

        if (info == null)
        {
            shell.WriteError($"Ошибка! GptChat: ответ = null");
            return;
        }

        foreach (var gptMsg in info.choices)
        {

            if (!_history.ContainsKey(userId))
            {
                return;
            }
            _history[userId].Add(new GptMessage(GptUserDirection.assistant, gptMsg.message.content));

            var bwoinkText = $"[color=lightblue]GptChat[/color]: {gptMsg.message.content}";

            var msg = new SharedBwoinkSystem.BwoinkTextMessage(userId, SharedBwoinkSystem.SystemUserId, bwoinkText);

            var admins = GetTargetAdmins();

            // Notify all admins
            foreach (var channel in admins)
            {
                RaiseNetworkEvent(msg, channel);
            }

            if (_playerManager.TryGetSessionById(userId, out var session))
            {
                if (!admins.Contains(session.ConnectedClient))
                    RaiseNetworkEvent(msg, session.ConnectedClient);
            }
        }



        shell.WriteLine("ГОТОВО!");
    }

    // Returns all online admins with AHelp access
    private IList<INetChannel> GetTargetAdmins()
    {
        return _adminManager.ActiveAdmins
            .Where(p => _adminManager.GetAdminData(p)?.HasFlag(AdminFlags.Adminhelp) ?? false)
            .Select(p => p.ConnectedClient)
            .ToList();
    }

    public override void Shutdown()
    {
        _cfg.UnsubValueChanged(Shared.Backmen.CCVar.CCVars.GptEnabled, GptEnabledCVarChanged);
        _cfg.UnsubValueChanged(Shared.Backmen.CCVar.CCVars.GptApiUrl, GptUrlCVarChanged);
        _cfg.UnsubValueChanged(Shared.Backmen.CCVar.CCVars.GptApiToken, GptTokenCVarChanged);

        base.Shutdown();
    }

    #region CVAR

    private void GptModelCVarChanged(string obj)
    {
        _apiModel = obj;
    }
    private void GptTokenCVarChanged(string obj)
    {
        _apiToken = obj;
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiToken);
    }

    private void GptUrlCVarChanged(string obj)
    {
        _apiUrl = obj;
    }

    private void GptEnabledCVarChanged(bool obj)
    {
        if (!obj)
        {
            _history.Clear();
        }

        _enabled = obj;
    }

    #endregion

    private void OnCleanup(RoundRestartCleanupEvent ev)
    {
        _history.Clear();
    }

    public void AddUserMessage(NetUserId messageUserId, bool personalChannel, string escapedText)
    {
        if (!_enabled)
        {
            return;
        }

        if (!_history.ContainsKey(messageUserId))
        {
            _history.Add(messageUserId, new List<GptMessage>());
        }

        _history[messageUserId]
            .Add(new GptMessage(personalChannel ? GptUserDirection.user : GptUserDirection.assistant, escapedText));

        if (_history[messageUserId].Count > 5)
        {
            _history[messageUserId].RemoveRange(0, _history[messageUserId].Count - 5);
        }
    }
}
