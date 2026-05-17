using Content.Server.Administration.Logs;
using Content.Server.Administration.Managers;
using Content.Server.Chat.Managers;
using Content.Server.Chat.Systems;
using Content.Shared.Backmen.Abilities.Psionics;
using Content.Shared.Bed.Sleep;
using Content.Shared.Chat;
using Content.Shared.Database;
using Content.Shared.Drugs;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Random;
using System.Linq;

using Content.Shared.Backmen.Psionics.Glimmer;
using Content.Shared.Item;
using Content.Shared.StatusEffectNew.Components;

namespace Content.Server.Backmen.Chat;

/// <summary>
/// Extensions for nyano's chat stuff
/// </summary>
public sealed partial class NyanoChatSystem : EntitySystem
{
    [Dependency] private IAdminManager _adminManager = default!;
    [Dependency] private IChatManager _chatManager = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IAdminLogManager _adminLogger = default!;
    [Dependency] private GlimmerSystem _glimmerSystem = default!;
    [Dependency] private ChatSystem _chatSystem = default!;
    [Dependency] private readonly EntityQuery<StatusEffectComponent> _statusQuery = default;
    [Dependency] private Shared.StatusEffectNew.StatusEffectsSystem _statusEffects = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AfterPsionicChat>(OnAfterPsiChat);
    }

    private void OnAfterPsiChat(AfterPsionicChat ev)
    {
        var clients = GetPsionicChatClients().ToArray();

        if (_random.Prob(0.1f))
            _glimmerSystem.Glimmer++;

        if (_random.Prob(Math.Min(0.33f + ((float) _glimmerSystem.Glimmer / 1500), 1)))
        {
            var obfuscation = (0.25f + (float) _glimmerSystem.Glimmer / 2000);
            var obfuscated = _chatSystem.ObfuscateMessageReadability(ev.Message, obfuscation);
            _chatManager.ChatMessageToMany(ChatChannel.Telepathic, obfuscated, ev.MessageWrap, ev.Source, ev.HideChat, false, GetDreamers(clients), Color.PaleVioletRed);
        }

        var once = new HashSet<EntityUid>();

        var q = EntityQueryEnumerator<TelepathicRepeaterComponent>();
        while (q.MoveNext(out var appliedTo, out _))
        {
            if (!once.Add(appliedTo))
                continue;

            if (_statusQuery.TryComp(appliedTo, out var statusEffectComponent) && statusEffectComponent.AppliedTo is {})
            {
                appliedTo = statusEffectComponent.AppliedTo.Value;

                if (!once.Add(appliedTo))
                    continue;
            }

            _chatSystem.TrySendInGameICMessage(appliedTo, ev.Message, InGameICChatType.Speak, false, true);
        }
    }

    private IEnumerable<INetChannel> GetPsionicChatClients()
    {
        return Filter.Empty()
            .AddWhereAttachedEntity(IsEligibleForTelepathy)
            .Recipients
            .Select(p => p.Channel);
    }

    private IEnumerable<INetChannel> GetAdminClients()
    {
        return _adminManager.ActiveAdmins
            .Select(p => p.Channel);
    }

    private List<INetChannel> GetDreamers(IEnumerable<INetChannel> removeList)
    {
        var filtered = Filter.Empty()
            .AddWhereAttachedEntity(entity =>
                 (HasComp<SleepingComponent>(entity) ||
                                 HasComp<SeeingRainbowsStatusEffectComponent>(entity)) &&
                !_statusEffects.HasEffectComp<PsionicInsulationComponent>(entity))
            .Recipients
            .Select(p => p.Channel);

        var filteredList = filtered.ToList();

        foreach (var entity in removeList)
        {
            filteredList.Remove(entity);
        }

        return filteredList;
    }

    private bool IsEligibleForTelepathy(EntityUid entity)
    {
        return HasComp<PsionicComponent>(entity)
               && !_statusEffects.HasEffectComp<PsionicInsulationComponent>(entity)
               && (!TryComp<MobStateComponent>(entity, out var mobstate) || mobstate.CurrentState == MobState.Alive);
    }

    public void SendTelepathicChat(EntityUid source, string message, bool hideChat)
    {
        if (!IsEligibleForTelepathy(source))
            return;

        if (!TryComp<PsionicComponent>(source, out var psionicComponent))
        {
            return;
        }

        var clients = GetPsionicChatClients().ToArray();
        var admins = GetAdminClients().ToArray();
        string messageWrap;
        string adminMessageWrap;

        var isPsionicChat = false;
        var channelname = "Telepathic";

        if (string.IsNullOrEmpty(psionicComponent.Channel))
        {
            messageWrap = Loc.GetString("chat-manager-send-telepathic-chat-wrap-message",
                ("telepathicChannelName", Loc.GetString("chat-manager-telepathic-channel-name")), ("message", message));

            adminMessageWrap = Loc.GetString("chat-manager-send-telepathic-chat-wrap-message-admin",
                ("source", source), ("message", message));

            isPsionicChat = true;
        }
        else
        {
            messageWrap = Loc.GetString($"chat-manager-send-{psionicComponent.Channel}-chat-wrap-message",
                ("telepathicChannelName", Loc.GetString($"chat-manager-{psionicComponent.Channel}-channel-name")), ("message", message));

            adminMessageWrap = Loc.GetString($"chat-manager-send-{psionicComponent.Channel}-chat-wrap-message-admin",
                ("source", source), ("message", message));

            isPsionicChat = false;
            channelname = psionicComponent.Channel;
        }

        _adminLogger.Add(LogType.Chat, LogImpact.Low, $"{channelname} chat from {ToPrettyString(source):Player}: {message}");

        _chatManager.ChatMessageToMany(ChatChannel.Telepathic, message, messageWrap, source, hideChat, true, clients.Where(x=>admins.All(z=>z.UserId != x.UserId)).ToList(), psionicComponent.ChannelColor);
        _chatManager.ChatMessageToMany(ChatChannel.Telepathic, message, adminMessageWrap, source, hideChat, true, admins, psionicComponent.ChannelColor);

        if (!isPsionicChat)
        {
            return;
        }

        QueueLocalEvent(new AfterPsionicChat
        {
            Source = source,
            Message = message,
            MessageWrap = messageWrap,
            HideChat = hideChat
        });
    }
}

public sealed class AfterPsionicChat : HandledEntityEventArgs
{
    public EntityUid Source { get; set; }
    public string MessageWrap { get; set; } = default!;
    public string Message { get; set; } = default!;
    public bool HideChat { get; set; }
}
