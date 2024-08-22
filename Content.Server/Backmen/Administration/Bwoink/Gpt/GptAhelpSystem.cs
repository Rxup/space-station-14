using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Content.Server.Administration;
using Content.Server.Administration.Managers;
using Content.Server.Backmen.Administration.Bwoink.Gpt.Models;
using Content.Shared.Administration;
using Content.Shared.GameTicking;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Console;
using Robust.Shared.Network;
using Robust.Shared.Utility;

namespace Content.Server.Backmen.Administration.Bwoink.Gpt;


public sealed class GptAhelpSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IConsoleHost _console = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IAdminManager _adminManager = default!;
    private readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromMinutes(3)
    };

    private Dictionary<NetUserId, GptUserInfo> _history = new();

    private bool _enabled = false;
    private string _apiUrl = "";
    private string _apiToken = "";
    private string _apiModel = "";
    private string _apiGigaToken = "";
    private DateTimeOffset _gigaTocExpire = DateTimeOffset.Now;

    private const string BotName = "GptChat";

    private List<object> _gptFunctions = new();
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnCleanup);
        _cfg.OnValueChanged(Shared.Backmen.CCVar.CCVars.GptEnabled, GptEnabledCVarChanged, true);
        _cfg.OnValueChanged(Shared.Backmen.CCVar.CCVars.GptApiUrl, GptUrlCVarChanged, true);
        _cfg.OnValueChanged(Shared.Backmen.CCVar.CCVars.GptApiToken, GptTokenCVarChanged, true);
        _cfg.OnValueChanged(Shared.Backmen.CCVar.CCVars.GptModel, GptModelCVarChanged, true);
        _cfg.OnValueChanged(Shared.Backmen.CCVar.CCVars.GptApiGigaToken, GptGigaTokenCVarChanged, true);

        _console.RegisterCommand("ahelp_gpt", GptCommand, GptCommandCompletion);
    }

    #region GigaChat

    private async Task UpdateGigaToken()
    {
        if(string.IsNullOrEmpty(_apiGigaToken))
            return;
        if(_gigaTocExpire > DateTimeOffset.Now)
            return;

        using var client = new HttpClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://ngw.devices.sberbank.ru:9443/api/v2/oauth");
        request.Headers.Add("Accept", "application/json");
        request.Headers.Add("RqUID", Guid.NewGuid().ToString());
        request.Headers.Add("Authorization", "Basic "+_apiGigaToken);
        var collection = new List<KeyValuePair<string, string>>();
        collection.Add(new("scope", "GIGACHAT_API_PERS"));
        var content = new FormUrlEncodedContent(collection);
        request.Content = content;
        using var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();


        var respBody = await response.Content.ReadAsStringAsync();
        var info = JsonSerializer.Deserialize<GigaTocResponse>(respBody);
        if (info == null)
        {
            Log.Debug(response.ToString());
            Log.Debug(respBody);
            return;
        }

        _gigaTocExpire = DateTimeOffset.FromUnixTimeMilliseconds(info.expires_at);
        GptTokenCVarChanged(info.access_token);
    }

    #endregion

    public void AddFunction(object model)
    {
        _gptFunctions.Add(model);
    }

    private CompletionResult GptCommandCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length != 1)
            return CompletionResult.Empty;

        return CompletionResult.FromHintOptions(CompletionHelper.SessionNames(), "Пользователь");
    }

    private void SetTyping(NetUserId channel, bool enable)
    {
        // Non-admins can only ever type on their own ahelp, guard against fake messages
        var update = new BwoinkPlayerTypingUpdated(channel, BotName, enable);

        var admins = GetTargetAdmins();

        foreach (var admin in admins)
        {
            RaiseNetworkEvent(update, admin);
        }

        if (_playerManager.TryGetSessionById(channel, out var session))
        {
            if (!admins.Contains(session.Channel))
                RaiseNetworkEvent(update, session.Channel);
        }
    }

    private async Task<(GptResponseApi? responseApi, string? err)> SendApiRequest(GptUserInfo history)
    {
        var payload = new GptApiPacket(_apiModel, history.GetMessagesForApi(), _gptFunctions,0.8f);
        var request = await _httpClient.PostAsync($"{_apiUrl}chat/completions",
            new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));

        var response = await request.Content.ReadAsStringAsync();

        if (!request.IsSuccessStatusCode)
        {
            Log.Debug(JsonSerializer.Serialize(payload));
            Log.Debug(request.StatusCode.ToString());
            Log.Debug(response);
            return (null, $"Ошибка! GptChat: {request.StatusCode} - {response}");
        }

        var info = JsonSerializer.Deserialize<GptResponseApi>(response);

        return (info, null);
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

        if (!_history.ContainsKey(userId) || _history[userId].Messages.Count == 0)
        {
            shell.WriteError("Пользователь не писал сообщений!");
            return;
        }

        var history = _history[userId];

        if (history.Lock.IsWriteLockHeld)
        {
            shell.WriteError("Уже в процессе!");
            return;
        }

        if (!history.IsCanAnswer())
        {
            shell.WriteError("Пользователь не писал сообщений!");
            return;
        }

        history.Lock.EnterWriteLock();
        try
        {
            await UpdateGigaToken();
            SetTyping(userId, true);

            await ProcessRequest(shell, userId, history);

            shell.WriteLine("ГОТОВО!");
        }
        finally
        {
            history.Lock.ExitWriteLock();
        }
    }

    private async Task ProcessRequest(IConsoleShell shell, NetUserId userId, GptUserInfo history)
    {
        var (info, err) = await SendApiRequest(history);
        if (!string.IsNullOrEmpty(err))
        {
            shell.WriteError(err);
            return;
        }
        if (info == null)
        {
            shell.WriteError($"Ошибка! GptChat: ответ = null");
            return;
        }

        await ProcessResponse(shell, userId, history, info);
    }

    private async Task ProcessResponse(IConsoleShell shell, NetUserId userId, GptUserInfo history, GptResponseApi info)
    {
        foreach (var gptMsg in info.choices)
        {
            if (!_history.ContainsKey(userId))
            {
                return;
            }

            if (gptMsg.finish_reason == "function_call")
            {
                await ProcessFunctionCall(shell, userId, history, gptMsg);
                break;
            }
            else if (gptMsg.finish_reason == "stop")
            {
                await ProcessChatResponse(shell, userId, history, gptMsg);
                break;
            }
        }
    }

    private async Task ProcessFunctionCall(IConsoleShell shell, NetUserId userId, GptUserInfo history, GptResponseApiChoice msg)
    {
        DebugTools.AssertNotNull(msg.message.function_call);

        var fnName = msg.message.function_call!.name;
        Log.Debug("FunctionCall {0} with {1}",fnName, msg.message.function_call.arguments);

        history.Add(new GptMessageCallFunction(msg.message));

        var ev = new EventGptFunctionCall(shell,userId,history,msg);
        RaiseLocalEvent(ev);

        if (!ev.Handled)
        {
            history.Add(new GptMessageFunction(fnName));
        }

        await ProcessRequest(shell, userId, history);
    }

    private async Task ProcessChatResponse(IConsoleShell shell, NetUserId userId, GptUserInfo history, GptResponseApiChoice gptMsg)
    {
        try
        {
            DebugTools.AssertNotNull(gptMsg.message.content);
            history.Messages.Add(new GptMessageChat(GptUserDirection.assistant, gptMsg.message.content!));

            var bwoinkText = $"[color=lightblue]{BotName}[/color]: {gptMsg.message.content}";

            var msg = new SharedBwoinkSystem.BwoinkTextMessage(userId, SharedBwoinkSystem.SystemUserId, bwoinkText);

            var admins = GetTargetAdmins();

            // Notify all admins
            foreach (var channel in admins)
            {
                RaiseNetworkEvent(msg, channel);
            }

            if (_playerManager.TryGetSessionById(userId, out var session))
            {
                if (!admins.Contains(session.Channel))
                    RaiseNetworkEvent(msg, session.Channel);
            }
        }
        finally
        {
            SetTyping(userId, false);
        }
    }

    // Returns all online admins with AHelp access
    private IList<INetChannel> GetTargetAdmins()
    {
        return _adminManager.ActiveAdmins
            .Where(p => _adminManager.GetAdminData(p)?.HasFlag(AdminFlags.Adminhelp) ?? false)
            .Select(p => p.Channel)
            .ToList();
    }

    public override void Shutdown()
    {
        _cfg.UnsubValueChanged(Shared.Backmen.CCVar.CCVars.GptEnabled, GptEnabledCVarChanged);
        _cfg.UnsubValueChanged(Shared.Backmen.CCVar.CCVars.GptApiUrl, GptUrlCVarChanged);
        _cfg.UnsubValueChanged(Shared.Backmen.CCVar.CCVars.GptApiToken, GptTokenCVarChanged);
        _cfg.UnsubValueChanged(Shared.Backmen.CCVar.CCVars.GptApiGigaToken, GptGigaTokenCVarChanged);

        base.Shutdown();
    }

    #region CVAR

    private void GptGigaTokenCVarChanged(string obj)
    {
        _apiGigaToken = obj;
    }

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

        _history.TryAdd(messageUserId, new GptUserInfo());

        _history[messageUserId].Add(new GptMessageChat(personalChannel ? GptUserDirection.user : GptUserDirection.assistant, escapedText));
    }
}

public sealed class EventGptFunctionCall : HandledEntityEventArgs
{
    public IConsoleShell Shell { get; }
    public NetUserId UserId { get; }
    public GptUserInfo History { get; }
    public GptResponseApiChoice Msg { get; }

    public EventGptFunctionCall(IConsoleShell shell, NetUserId userId, GptUserInfo history, GptResponseApiChoice msg)
    {
        Shell = shell;
        UserId = userId;
        History = history;
        Msg = msg;
    }

}
