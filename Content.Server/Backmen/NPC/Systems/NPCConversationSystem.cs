using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text;
using System.Text.Json;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Content.Server.Backmen.NPC.Components;
using Content.Server.Backmen.NPC.Events;
using Content.Server.Backmen.Administration.Bwoink.Gpt.Models;
using Content.Shared.GameTicking;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Content.Server.Chat.Systems;
using Content.Server.Chat.TypingIndicator;
using Content.Server.Backmen.NPC.Prototypes;
using Content.Server.NPC.Systems;
using Content.Shared.Chat;
using Content.Shared.Chat.TypingIndicator;
using Content.Shared.Interaction;
using Content.Shared.Speech;
using Robust.Server.Audio;
using Robust.Shared.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Utility;

namespace Content.Server.Backmen.NPC.Systems;

public sealed class NPCConversationSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly AudioSystem _audioSystem = default!;
    [Dependency] private readonly ChatSystem _chatSystem = default!;
    [Dependency] private readonly NPCSystem _npcSystem = default!;
    [Dependency] private readonly RotateToFaceSystem _rotateToFaceSystem = default!;
    [Dependency] private readonly TransformSystem _transformSystem = default!;
    [Dependency] private readonly TypingIndicatorSystem _typingIndicatorSystem = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    private ISawmill _sawmill = default!;

    // GPT Integration
    private Dictionary<EntityUid, NPCGptHistory> _npcGptHistory = new();
    private List<object> _gptFunctions = new();
    private static readonly SocketsHttpHandler _gptSocketsHttpHandler = new()
    {
        SslOptions = new SslClientAuthenticationOptions
        {
            RemoteCertificateValidationCallback = (sender, cert, chain, sslPolicyErrors) =>
            {
                // Разрешаем, если ошибок нет
                if (sslPolicyErrors == SslPolicyErrors.None)
                    return true;

                // Разрешаем только если единственная ошибка - UntrustedRoot
                if (sslPolicyErrors == SslPolicyErrors.RemoteCertificateChainErrors && chain?.ChainStatus is {} chainStatus)
                {
                    // Проверяем все статусы в цепочке
                    foreach (var status in chainStatus)
                    {
                        // Если есть ошибка, отличная от UntrustedRoot - блокируем
                        if (status.Status != X509ChainStatusFlags.UntrustedRoot)
                            return false;
                    }
                    return true; // Только UntrustedRoot - пропускаем
                }
                return false; // Другие ошибки (недействительное имя, просроченный и т.д.)
            }
        }
    };
    private readonly HttpClient _gptHttpClient = new(_gptSocketsHttpHandler, false)
    {
        Timeout = TimeSpan.FromMinutes(3),
    };
    private string _gptApiUrl = "";
    private string _gptApiToken = "";
    private string _gptApiModel = "";
    private string _gptApiGigaToken = "";
    private DateTimeOffset _gigaTocExpire = DateTimeOffset.Now;
    private bool _gptEnabled = false;

    // TODO: attention attenuation. distance, facing, visible
    // TODO: attending to multiple people, multiple streams of conversation
    // TODO: multi-word prompts
    // TODO: nameless prompting (pointing is good)
    // TODO: aliases

    public static readonly string[] QuestionWords = { "who", "what", "when", "why", "where", "how", "кто", "это", "тогда", "почему", "где", "как" };
    public static readonly string[] Copulae = { "is", "are", "является", "есть", "что" };

    public override void Initialize()
    {
        base.Initialize();

        _sawmill = Logger.GetSawmill("npc.conversation");

        SubscribeLocalEvent<NPCConversationComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<NPCConversationComponent, ComponentRemove>(OnComponentRemove);
        SubscribeLocalEvent<NPCConversationComponent, EntityUnpausedEvent>(OnUnpaused);
        SubscribeLocalEvent<NPCConversationComponent, ListenAttemptEvent>(OnListenAttempt);
        SubscribeLocalEvent<NPCConversationComponent, ListenEvent>(OnListen);

        SubscribeLocalEvent<NPCConversationComponent, NPCConversationByeEvent>(OnBye);
        SubscribeLocalEvent<NPCConversationComponent, NPCConversationHelpEvent>(OnHelp);

        SubscribeLocalEvent<NPCConversationComponent, NPCConversationToldNameEvent>(OnToldName);

        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);

        SubscribeLocalEvent<EventGptFunctionCallNPC>(OnGptFunctionCall);

        // Initialize GPT
        InitializeGpt();
        InitializeGptFunctions();
    }

    private void InitializeGptFunctions()
    {
        // Функция для открытия/закрытия дверей
        AddFunction(new
        {
            name = "GetAvailableTopics",
            description = "Получить список всех доступных диалоговых опций (тем для разговора) для этого конкретного NPC. Используй эту функцию чтобы узнать, какие темы можно обсудить с игроком. В ответе будет информация о заблокированных диалогах и событиях, которые могут сработать при выборе диалога.",
            parameters = new
            {
                @type = "object",
                properties = new { },
                required = new string[] { }
            }
        });
    }

    /// <summary>
    /// Добавить функцию для GPT API.
    /// </summary>
    public void AddFunction(object functionModel)
    {
        _gptFunctions.Add(functionModel);
    }

    private void InitializeGpt()
    {
        _cfg.OnValueChanged(Shared.Backmen.CCVar.CCVars.GptEnabled, GptEnabledCVarChanged, true);
        _cfg.OnValueChanged(Shared.Backmen.CCVar.CCVars.GptApiUrl, GptUrlCVarChanged, true);
        _cfg.OnValueChanged(Shared.Backmen.CCVar.CCVars.GptApiToken, GptTokenCVarChanged, true);
        _cfg.OnValueChanged(Shared.Backmen.CCVar.CCVars.GptModel, GptModelCVarChanged, true);
        _cfg.OnValueChanged(Shared.Backmen.CCVar.CCVars.GptApiGigaToken, GptGigaTokenCVarChanged, true);
    }

    private void GptEnabledCVarChanged(bool obj)
    {
        _gptEnabled = obj;
        if (!obj)
        {
            _npcGptHistory.Clear();
        }
    }

    private void GptUrlCVarChanged(string obj)
    {
        _gptApiUrl = obj;
    }

    private void GptTokenCVarChanged(string obj)
    {
        _gptApiToken = obj;
        if (_gptHttpClient != null)
        {
            _gptHttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _gptApiToken);
        }
    }

    private void GptModelCVarChanged(string obj)
    {
        _gptApiModel = obj;
    }

    private void GptGigaTokenCVarChanged(string obj)
    {
        _gptApiGigaToken = obj;
    }

#region API

    /// <summary>
    /// Toggle the ability of an NPC to listen for topics.
    /// </summary>
    public void EnableConversation(Entity<NPCConversationComponent?> uid, bool enable = true)
    {
        if (!Resolve(uid, ref uid.Comp))
            return;

        uid.Comp.Enabled = enable;
    }

    /// <summary>
    /// Toggle the NPC's willingness to make idle comments.
    /// </summary>
    public void EnableIdleChat(Entity<NPCConversationComponent?> uid, bool enable = true)
    {
        if (!Resolve(uid, ref uid.Comp))
            return;

        uid.Comp.IdleEnabled = enable;
    }

    /// <summary>
    /// Return locked status of a dialogue topic.
    /// </summary>
    public bool IsDialogueLocked(Entity<NPCConversationComponent?> uid, string option)
    {
        if (!Resolve(uid, ref uid.Comp))
            return true;

        if (!uid.Comp.ConversationTree.PromptToTopic.TryGetValue(option, out var topic))
        {
            _sawmill.Warning($"Tried to check locked status of missing dialogue option `{option}` on {ToPrettyString(uid)}");
            return true;
        }

        if (uid.Comp.UnlockedTopics.Contains(topic))
            return false;

        return topic.Locked;
    }

    /// <summary>
    /// Unlock dialogue options normally locked in an NPC's conversation tree.
    /// </summary>
    public void UnlockDialogue(Entity<NPCConversationComponent?> uid, string option)
    {
        if (!Resolve(uid, ref uid.Comp))
            return;

        if (uid.Comp.ConversationTree.PromptToTopic.TryGetValue(option, out var topic))
            uid.Comp.UnlockedTopics.Add(topic);
        else
            _sawmill.Warning($"Tried to unlock missing dialogue option `{option}` on {ToPrettyString(uid)}");
    }

    /// <inheritdoc cref="UnlockDialogue(EntityUid, string, NPCConversationComponent?)"/>
    public void UnlockDialogue(Entity<NPCConversationComponent?> uid, HashSet<string> options)
    {
        if (!Resolve(uid, ref uid.Comp))
            return;

        foreach (var option in options)
        {
            UnlockDialogue(uid, option);
        }
    }

    /// <summary>
    /// Queue a response for an NPC with a visible typing indicator and delay between messages.
    /// </summary>
    /// <remarks>
    /// This can be used as opposed to the typical <see cref="ChatSystem.TrySendInGameICMessage"/> method.
    /// </remarks>
    public void QueueResponse(Entity<NPCConversationComponent?> uid, NPCResponse response)
    {
        if (!Resolve(uid, ref uid.Comp))
            return;

        if (response.Is is {} ev)
        {
            // This is a dynamic response which will call QueueResponse with static responses of its own.
            ev.TalkingTo = uid.Comp.AttendingTo;
            RaiseLocalEvent(uid, (object) ev);
            return;
        }

        if (uid.Comp.ResponseQueue.Count == 0)
        {
            DelayResponse(uid!, response);
            _typingIndicatorSystem.SetTypingIndicatorState(uid, TypingIndicatorState.Typing);
        }

        uid.Comp.ResponseQueue.Push(response);
    }

    /// <summary>
    /// Make an NPC stop paying attention to someone.
    /// </summary>
    public void LoseAttention(Entity<NPCConversationComponent?> uid)
    {
        if (!Resolve(uid, ref uid.Comp))
            return;

        uid.Comp.AttendingTo = null;
        uid.Comp.ListeningEvent = null;
        _rotateToFaceSystem.TryFaceAngle(uid, uid.Comp.OriginalFacing);
    }

#endregion

    private void DelayResponse(Entity<NPCConversationComponent> uid, NPCResponse response)
    {
        if (response.Text == null)
            return;

        uid.Comp.NextResponse = _gameTiming.CurTime +
                                uid.Comp.DelayBeforeResponse +
                                uid.Comp.TypingDelay.TotalSeconds * TimeSpan.FromSeconds(response.Text.Length) *
                                _random.NextDouble(0.9, 1.1);
    }

    private IEnumerable<NPCTopic> GetAvailableTopics(Entity<NPCConversationComponent> uid)
    {
        HashSet<NPCTopic> availableTopics = new();

        foreach (var topic in uid.Comp.ConversationTree.Dialogue)
        {
            if (!topic.Locked || uid.Comp.UnlockedTopics.Contains(topic))
                availableTopics.Add(topic);
        }

        return availableTopics;
    }

    private IEnumerable<NPCTopic> GetVisibleTopics(Entity<NPCConversationComponent> uid)
    {
        HashSet<NPCTopic> visibleTopics = new();

        foreach (var topic in uid.Comp.ConversationTree.Dialogue)
        {
            if (!topic.Hidden && (!topic.Locked || uid.Comp.UnlockedTopics.Contains(topic)))
                visibleTopics.Add(topic);
        }

        return visibleTopics;
    }

    private void OnInit(EntityUid uid, NPCConversationComponent component, ComponentInit args)
    {
        if (component.ConversationTreeId == null)
            return;

        component.ConversationTree = _prototype.Index<NPCConversationTreePrototype>(component.ConversationTreeId);
        component.NextIdleChat = _gameTiming.CurTime + component.IdleChatDelay;

        for (var i = 0; i < component.ConversationTree.Idle.Length; ++i)
        {
            component.IdleChatOrder.Add(i);
        }

        _random.Shuffle(component.IdleChatOrder);
    }

    private void OnUnpaused(EntityUid uid, NPCConversationComponent component, ref EntityUnpausedEvent args)
    {
        component.NextResponse += args.PausedTime;
        component.NextAttentionLoss += args.PausedTime;
        component.NextIdleChat += args.PausedTime;
    }

    private void OnComponentRemove(EntityUid uid, NPCConversationComponent component, ComponentRemove args)
    {
        // Clean up GPT history when component is removed
        _npcGptHistory.Remove(uid);
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        _npcGptHistory.Clear();
    }

    private bool TryGetIdleChatLine(Entity<NPCConversationComponent> uid, [NotNullWhen(true)] out NPCResponse? line)
    {
        line = null;

        if (!uid.Comp.IdleChatOrder.Any())
            return false;

        if (++uid.Comp.IdleChatIndex == uid.Comp.IdleChatOrder.Count())
        {
            // Exhausted all lines in the pre-shuffled order.
            // Reset the index and shuffle again.
            uid.Comp.IdleChatIndex = 0;
            _random.Shuffle(uid.Comp.IdleChatOrder);
        }

        var index = uid.Comp.IdleChatOrder[uid.Comp.IdleChatIndex];

        line = uid.Comp.ConversationTree.Idle[index];

        return true;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<NPCConversationComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            Entity<NPCConversationComponent> ent = (uid, component);
            var curTime = _gameTiming.CurTime;

            if (curTime >= component.NextResponse && component.ResponseQueue.Count > 0)
            {
                // Make a response.
                Respond(ent, component.ResponseQueue.Pop());
            }

            if (curTime >= component.NextAttentionLoss && component.AttendingTo != null)
            {
                // Forget who we were talking to.
                LoseAttention(uid);
            }

            if (component.IdleEnabled &&
                curTime >= component.NextIdleChat &&
                TryGetIdleChatLine(ent, out var line))
            {
                Respond(ent, line);
            }
        }
    }

    private void OnListenAttempt(EntityUid uid, NPCConversationComponent component, ListenAttemptEvent args)
    {
        if (!component.Enabled ||
            // Don't listen to myself...
            uid == args.Source ||
            // Don't bother listening to other NPCs. For now.
            !HasComp<ActorComponent>(args.Source) ||
            // We're already "typing" a response, so do that first.
            component.ResponseQueue.Count > 0)
        {
            args.Cancel();
        }
    }

    private void PayAttentionTo(Entity<NPCConversationComponent> uid, EntityUid speaker)
    {
        uid.Comp.AttendingTo = speaker;
        uid.Comp.NextAttentionLoss = _gameTiming.CurTime + uid.Comp.AttentionSpan;
        uid.Comp.OriginalFacing = _transformSystem.GetWorldRotation(uid);
    }

    private void Respond(Entity<NPCConversationComponent> uid, NPCResponse response)
    {
        if (uid.Comp.ResponseQueue.Count == 0)
            _typingIndicatorSystem.SetTypingIndicatorState(uid, TypingIndicatorState.None);
        else
            DelayResponse(uid, uid.Comp.ResponseQueue.Peek());

        if (uid.Comp.AttendingTo != null)
        {
            // TODO: This line is a mouthful. Maybe write a public API that supports EntityCoordinates later?
            var speakerCoords = Transform(uid.Comp.AttendingTo.Value).Coordinates.ToMap(EntityManager, _transformSystem).Position;
            _rotateToFaceSystem.TryFaceCoordinates(uid, speakerCoords);
        }

        if (response.Event is {} ev)
        {
            ev.TalkingTo = uid.Comp.AttendingTo;
            RaiseLocalEvent(uid, (object) ev);
        }

        if (response.ListenEvent != null)
            uid.Comp.ListeningEvent = response.ListenEvent;

        if (response.Text != null)
            _chatSystem.TrySendInGameICMessage(uid, Loc.GetString(response.Text), InGameICChatType.Speak, false);

        if (response.Audio != null)
            _audioSystem.PlayPvs(response.Audio, uid,
                // TODO: Allow this to be configured per NPC/response.
                AudioParams.Default
                    .WithVolume(8f)
                    .WithMaxDistance(9f)
                    .WithRolloffFactor(0.5f));

        // Refresh our attention.
        uid.Comp.NextAttentionLoss = _gameTiming.CurTime + uid.Comp.AttentionSpan;
        uid.Comp.NextIdleChat = uid.Comp.NextAttentionLoss + uid.Comp.IdleChatDelay;
    }

    private List<string> ParseMessageIntoWords(string message)
    {
        return Regex.Replace(message.Trim().ToLower(), @"(\p{P})", "")
            .Split()
            .ToList();
    }

    private bool FindResponse(Entity<NPCConversationComponent> uid, List<string> words, [NotNullWhen(true)] out NPCResponse? response)
    {
        response = null;

        var availableTopics = GetAvailableTopics(uid).ToArray();

        // Some topics are more interesting than others.
        var greatestWeight = 0f;
        NPCTopic? candidate = null;

        foreach (var word in words)
        {
            if (uid.Comp.ConversationTree.PromptToTopic.TryGetValue(word, out var topic) &&
                availableTopics.Contains(topic) &&
                topic.Weight > greatestWeight)
            {
                greatestWeight = topic.Weight;
                candidate = topic;
            }
        }

        if (candidate != null)
        {
            response = _random.Pick(candidate.Responses);
            return true;
        }

        return false;
    }

    private bool JudgeQuestionLikelihood(Entity<NPCConversationComponent> uid, List<string> words, string message)
    {
        if (message.Length > 0 && message[^1] == '?')
            // A question mark is an absolute mark of a question.
            return true;

        if (words.Count == 1)
            // The usefulness of this is dubious, but it's definitely a question.
            return QuestionWords.Contains(words[0]);

        if (words.Count >= 2)
            return QuestionWords.Contains(words[0]) && Copulae.Contains(words[1]);

        return false;
    }

    private void OnBye(EntityUid uid, NPCConversationComponent component, NPCConversationByeEvent args)
    {
        LoseAttention((uid, component));
    }

    private void OnHelp(EntityUid uid, NPCConversationComponent component, NPCConversationHelpEvent args)
    {
        if (args.Text == null)
        {
            _sawmill.Error($"{ToPrettyString(uid)} heard a Help prompt but has no text for it.");
            return;
        }

        var availableTopics = GetVisibleTopics((uid, component));
        var availablePrompts = availableTopics.Select(topic => topic.Prompts.FirstOrDefault()).ToArray();

        string availablePromptsText;
        if (availablePrompts.Count() <= 2)
        {
            availablePromptsText = Loc.GetString(args.Text,
                ("availablePrompts", string.Join(Loc.GetString("sophia-or"), availablePrompts.Select(x=>Loc.GetString("sophia-topic-"+x))))
            );
        }
        else
        {
            availablePrompts[^1] = $"{Loc.GetString("sophia-or")} {availablePrompts[^1]}";
            availablePromptsText = Loc.GetString(args.Text,
                ("availablePrompts", string.Join(", ", availablePrompts))
            );
        }

        // Unlikely we'll be able to do audio that isn't hard-coded,
        // so best to keep it general.
        var response = new NPCResponse(availablePromptsText, args.Audio);
        QueueResponse((uid, component), response);
    }

    private void OnToldName(EntityUid uid, NPCConversationComponent component, NPCConversationListenEvent args)
    {
        if (!component.ConversationTree.Custom.TryGetValue("toldName", out var responses))
            return;

        var response = _random.Pick(responses);
        if (response.Text == null)
        {
            _sawmill.Error($"{ToPrettyString(uid)} was told a name but had no text response.");
            return;
        }

        // The world's simplest heuristic for names:
        if (args.Words.Count > 3)
        {
            // It didn't seem like a name, so wait for something that does.
            return;
        }

        var cleanedName = string.Join(" ", args.Words);
        cleanedName = char.ToUpper(cleanedName[0]) + cleanedName.Remove(0, 1);

        var formattedResponse = new NPCResponse(Loc.GetString(response.Text,
                ("name", cleanedName)),
                response.Audio);

        QueueResponse((uid, component), formattedResponse);
        args.Handled = true;
    }

    private void OnListen(Entity<NPCConversationComponent> uid, ref ListenEvent args)
    {
        if (uid.Comp.AttendingTo != null && uid.Comp.AttendingTo != args.Source)
            // Ignore someone speaking to us if we're already paying attention to someone else.
            return;

        var words = ParseMessageIntoWords(args.Message);
        if (words.Count == 0)
            return;

        if (uid.Comp.AttendingTo == args.Source)
        {
            // The person we're talking to said something to us.

            if (uid.Comp.ListeningEvent is {} ev)
            {
                // We were waiting on this person to say something, and they've said something.
                ev.Handled = false;
                ev.Speaker = uid.Comp.AttendingTo;
                ev.Message = args.Message;
                ev.Words = words;
                RaiseLocalEvent(uid, (object) ev);

                if (ev.Handled)
                    uid.Comp.ListeningEvent = null;

                return;
            }

            // We're already paying attention to this person,
            // so try to figure out if they said something we can talk about.
            if (FindResponse(uid, words, out var response))
            {
                // A response was found so go ahead with it.
                QueueResponse(uid.AsNullable(), response);
            }
            else if(JudgeQuestionLikelihood(uid, words, args.Message))
            {
                // The message didn't match any of the prompts, but it seemed like a question.
                // Try GPT if enabled, otherwise use unknown response
                if (uid.Comp.UseGpt && _gptEnabled)
                {
                    _ = TryGptResponse(uid, args.Message, args.Source);
                }
                else
                {
                    var unknownResponse = _random.Pick(uid.Comp.ConversationTree.Unknown);
                    QueueResponse(uid.AsNullable(), unknownResponse);
                }
            }
            else if (uid.Comp.UseGpt && _gptEnabled)
            {
                // If GPT is enabled and no response found, try GPT anyway
                _ = TryGptResponse(uid, args.Message, args.Source);
            }

            // If the message didn't seem like a question,
            // and it didn't raise any of our topics,
            // and GPT is not enabled or failed,
            // then politely ignore who we're talking with.
            //
            // It's better than spamming them with "I don't understand."
            return;
        }

        // See if someone said our name.
        var myName = MetaData(uid).EntityName.ToLower();

        // So this is a rough heuristic, but if our name occurs within the first three words,
        // or is the very last one, someone might be trying to talk to us.
        var payAttention = words[0] == myName || words[^1] == myName;
        if (!payAttention)
        {
            for (int i = 1; i < Math.Min(2, words.Count); ++i)
            {
                if (words[i] == myName)
                {
                    payAttention = true;
                    break;
                }
            }
        }

        if (payAttention)
        {
            PayAttentionTo(uid, args.Source);

            if (!FindResponse(uid, words, out var response))
            {
                if(JudgeQuestionLikelihood(uid, words, args.Message) &&
                    // This subcondition exists to block our name being interpreted as a question in its own right.
                    words.Count > 1)
                {
                    // Try GPT if enabled, otherwise use unknown response
                    if (uid.Comp.UseGpt && _gptEnabled)
                    {
                        _ = TryGptResponse(uid, args.Message, args.Source);
                        return; // GPT will handle the response
                    }
                    response = _random.Pick(uid.Comp.ConversationTree.Unknown);
                }
                else
                {
                    response = _random.Pick(uid.Comp.ConversationTree.Attention);
                }
            }

            QueueResponse(uid.AsNullable(), response);
        }
    }

#region GPT Integration

    /// <summary>
    /// History of GPT messages for an NPC.
    /// </summary>
    public sealed class NPCGptHistory
    {
        public List<GptMessage> Messages { get; } = new();
        public readonly ReaderWriterLockSlim Lock = new();

        public NPCGptHistory(string systemPrompt)
        {
            Lock.EnterWriteLock();
            try
            {
                if (!string.IsNullOrEmpty(systemPrompt))
                {
                    Messages.Add(new GptMessageChat(GptUserDirection.system, systemPrompt));
                }
            }
            finally
            {
                Lock.ExitWriteLock();
            }
        }

        public void Add(GptMessage msg)
        {
            Messages.Add(msg);

            if (Messages.Count > 100)
            {
                Messages.RemoveRange(0, Messages.Count - 100);
            }
        }

        public bool IsCanAnswer()
        {
            return Messages.Count > 0 && Messages.Last().Role == GptUserDirection.user;
        }

        public object[] GetMessagesForApi()
        {
            return Messages.Select(x => x.ToApi()).ToArray();
        }
    }

    private NPCGptHistory GetOrCreateGptHistory(Entity<NPCConversationComponent> uid)
    {
        if (!_npcGptHistory.TryGetValue(uid, out var history))
        {
            var systemPrompt = uid.Comp.GptSystemPrompt ??
                $"Ты NPC в игре Space Station 14. Твое имя: {Name(uid)}. " +
                $"Отвечай кратко и в соответствии с ролью персонажа.";
            history = new NPCGptHistory(systemPrompt);
            _npcGptHistory[uid] = history;
        }
        return history;
    }

    #region GigaChat

    private async Task UpdateGigaToken()
    {
        if(string.IsNullOrEmpty(_gptApiGigaToken))
            return;
        if(_gigaTocExpire > DateTimeOffset.Now)
            return;

        using var client = new HttpClient(_gptSocketsHttpHandler, false);
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://ngw.devices.sberbank.ru:9443/api/v2/oauth");
        request.Headers.Add("Accept", "application/json");
        request.Headers.Add("RqUID", Guid.NewGuid().ToString());
        request.Headers.Add("Authorization", "Basic "+_gptApiGigaToken);
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
            _sawmill.Debug($"GigaChat token response: {response}");
            _sawmill.Debug($"GigaChat token body: {respBody}");
            return;
        }

        _gigaTocExpire = DateTimeOffset.FromUnixTimeMilliseconds(info.expires_at);
        GptTokenCVarChanged(info.access_token);
    }

    #endregion

    private async Task<(GptResponseApi? responseApi, string? err)> SendGptApiRequest(NPCGptHistory history, EntityUid uid)
    {
        if (!_gptEnabled || string.IsNullOrEmpty(_gptApiUrl) || string.IsNullOrEmpty(_gptApiToken) || string.IsNullOrEmpty(_gptApiModel))
        {
            return (null, "GPT не настроен или отключен");
        }

        // Обновляем токен GigaChat если нужно
        await UpdateGigaToken();

        history.Lock.EnterReadLock();
        try
        {
            var payload = new GptApiPacket(_gptApiModel, history.GetMessagesForApi(), _gptFunctions, 0.8f);
            var postData = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            postData.Headers.Add("X-Session-ID", uid.ToString());

            if (!string.IsNullOrEmpty(_gptApiToken))
            {
                _gptHttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _gptApiToken);
            }

            var request = await _gptHttpClient.PostAsync($"{_gptApiUrl + (_gptApiUrl.EndsWith("/") ? "" : "/")}chat/completions", postData);
            var response = await request.Content.ReadAsStringAsync();

            if (!request.IsSuccessStatusCode)
            {
                _sawmill.Warning($"GPT API ошибка для {ToPrettyString(uid)}: {request.StatusCode} - {response}");
                return (null, $"Ошибка GPT API: {request.StatusCode}");
            }

            var info = JsonSerializer.Deserialize<GptResponseApi>(response);
            return (info, null);
        }
        finally
        {
            history.Lock.ExitReadLock();
        }
    }

    private async Task ProcessGptResponse(Entity<NPCConversationComponent> uid, NPCGptHistory history, GptResponseApi info)
    {
        foreach (var gptMsg in info.choices)
        {
            switch (gptMsg.finish_reason)
            {
                case "function_call":
                    await ProcessFunctionCall(uid, history, gptMsg);
                    break;
                case "stop":
                case "length":
                case "blacklist":
                    if (gptMsg.message.content != null)
                    {
                        history.Lock.EnterWriteLock();
                        try
                        {
                            history.Add(new GptMessageChat(GptUserDirection.assistant, gptMsg.message.content));
                        }
                        finally
                        {
                            history.Lock.ExitWriteLock();
                        }

                        var response = new NPCResponse(gptMsg.message.content, null);
                        QueueResponse(uid.AsNullable(), response);
                        uid.Comp.GptProcessing = false;
                    }
                    break;
                default:
                    _sawmill.Warning($"GPT вернул неподдерживаемый finish_reason: {gptMsg.finish_reason} для {ToPrettyString(uid)}");
                    uid.Comp.GptProcessing = false;
                    break;
            }
        }
    }

    private async Task ProcessFunctionCall(Entity<NPCConversationComponent> uid, NPCGptHistory history, GptResponseApiChoice msg)
    {
        DebugTools.AssertNotNull(msg.message.function_call);

        var fnName = msg.message.function_call!.name;
        _sawmill.Debug($"NPC {ToPrettyString(uid)} FunctionCall {fnName} with {msg.message.function_call.arguments}");

        history.Lock.EnterWriteLock();
        try
        {
            history.Add(new GptMessageCallFunction(msg.message));
        }
        finally
        {
            history.Lock.ExitWriteLock();
        }

        var ev = new EventGptFunctionCallNPC(uid, history, msg);
        RaiseLocalEvent(ev);

        if (ev is { Handled: false, HandlerTask: null })
        {
            history.Lock.EnterWriteLock();
            try
            {
                history.Add(new GptMessageFunction(fnName));
            }
            finally
            {
                history.Lock.ExitWriteLock();
            }
        }
        else if (ev.HandlerTask != null)
        {
            try
            {
                await ev.HandlerTask;
            }
            catch (Exception e)
            {
                _sawmill.Error($"Ошибка при выполнении функции {fnName} для {ToPrettyString(uid)}: {e}");
                history.Lock.EnterWriteLock();
                try
                {
                    history.Add(new GptMessageFunction(fnName));
                }
                finally
                {
                    history.Lock.ExitWriteLock();
                }
            }
            if (!ev.Handled)
            {
                history.Lock.EnterWriteLock();
                try
                {
                    history.Add(new GptMessageFunction(fnName));
                }
                finally
                {
                    history.Lock.ExitWriteLock();
                }
            }
        }

        // Продолжаем обработку после вызова функции
        var (responseApi, err) = await SendGptApiRequest(history, uid);
        if (err != null || responseApi == null)
        {
            _sawmill.Warning($"Не удалось получить ответ от GPT после функции для {ToPrettyString(uid)}: {err}");
            uid.Comp.GptProcessing = false;
            return;
        }

        await ProcessGptResponse(uid, history, responseApi);
    }

    private async Task TryGptResponse(Entity<NPCConversationComponent> uid, string message, EntityUid speaker)
    {
        if (!uid.Comp.UseGpt || uid.Comp.GptProcessing || !_gptEnabled)
            return;

        uid.Comp.GptProcessing = true;
        try
        {
            var history = GetOrCreateGptHistory(uid);

            history.Lock.EnterWriteLock();
            try
            {
                var speakerName = MetaData(speaker).EntityName;
                history.Add(new GptMessageChat(GptUserDirection.user, $"{speakerName}: {message}"));
            }
            finally
            {
                history.Lock.ExitWriteLock();
            }

            var (responseApi, err) = await SendGptApiRequest(history, uid);

            if (err != null || responseApi == null)
            {
                _sawmill.Warning($"Не удалось получить ответ от GPT для {ToPrettyString(uid)}: {err}");
                uid.Comp.GptProcessing = false;
                return;
            }

            await ProcessGptResponse(uid, history, responseApi);
        }
        catch (Exception ex)
        {
            _sawmill.Error($"Исключение при обработке GPT запроса для {ToPrettyString(uid)}: {ex}");
            uid.Comp.GptProcessing = false;
        }
    }

    private void OnGptFunctionCall(EventGptFunctionCallNPC ev)
    {
        if (ev.Handled)
            return;

        var fnName = ev.Msg.message.function_call?.name;
        switch (fnName)
        {
            case "GetAvailableTopics":
                ev.History.Lock.EnterWriteLock();
                try
                {
                    ev.History.Add(new GptMessageFunction("GetAvailableTopics", new { result = GetAvailableTopics(ev.NpcUid).ToArray() }));
                }
                finally
                {
                    ev.History.Lock.ExitWriteLock();
                }
                ev.Handled = true;
                break;
        }
    }

    public override void Shutdown()
    {
        _cfg.UnsubValueChanged(Shared.Backmen.CCVar.CCVars.GptEnabled, GptEnabledCVarChanged);
        _cfg.UnsubValueChanged(Shared.Backmen.CCVar.CCVars.GptApiUrl, GptUrlCVarChanged);
        _cfg.UnsubValueChanged(Shared.Backmen.CCVar.CCVars.GptApiToken, GptTokenCVarChanged);
        _cfg.UnsubValueChanged(Shared.Backmen.CCVar.CCVars.GptModel, GptModelCVarChanged);
        _cfg.UnsubValueChanged(Shared.Backmen.CCVar.CCVars.GptApiGigaToken, GptGigaTokenCVarChanged);

        base.Shutdown();
    }

#endregion
}

/// <summary>
/// Событие для обработки вызова функции GPT для NPC.
/// </summary>
public sealed class EventGptFunctionCallNPC : HandledEntityEventArgs
{
    public Task? HandlerTask { get; set; }
    public Entity<NPCConversationComponent> NpcUid { get; }
    public NPCConversationSystem.NPCGptHistory History { get; }
    public GptResponseApiChoice Msg { get; }

    public EventGptFunctionCallNPC(Entity<NPCConversationComponent> npcUid, NPCConversationSystem.NPCGptHistory history, GptResponseApiChoice msg)
    {
        NpcUid = npcUid;
        History = history;
        Msg = msg;
    }
}
