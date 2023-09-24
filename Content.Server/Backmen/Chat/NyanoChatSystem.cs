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

namespace Content.Server.Backmen.Chat;

/// <summary>
/// Extensions for nyano's chat stuff
/// </summary>
public sealed class NyanoChatSystem : EntitySystem
{
    [Dependency] private readonly IAdminManager _adminManager = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IAdminLogManager _adminLogger = default!;
    [Dependency] private readonly GlimmerSystem _glimmerSystem = default!;
    [Dependency] private readonly ChatSystem _chatSystem = default!;
    private IEnumerable<INetChannel> GetPsionicChatClients()
    {
        return Filter.Empty()
            .AddWhereAttachedEntity(IsEligibleForTelepathy)
            .Recipients
            .Select(p => p.ConnectedClient);
    }

    private IEnumerable<INetChannel> GetAdminClients()
    {
        return _adminManager.ActiveAdmins
            .Select(p => p.ConnectedClient);
    }

    private List<INetChannel> GetDreamers(IEnumerable<INetChannel> removeList)
    {
        var filtered = Filter.Empty()
            .AddWhereAttachedEntity(entity => HasComp<SleepingComponent>(entity) || HasComp<SeeingRainbowsComponent>(entity) && !HasComp<PsionicsDisabledComponent>(entity) && !HasComp<PsionicInsulationComponent>(entity))
            .Recipients
            .Select(p => p.ConnectedClient);

        var filteredList = filtered.ToList();

        foreach (var entity in removeList)
            filteredList.Remove(entity);

        return filteredList;
    }

    private bool IsEligibleForTelepathy(EntityUid entity)
    {
        return HasComp<PsionicComponent>(entity)
               && !HasComp<PsionicsDisabledComponent>(entity)
               && !HasComp<PsionicInsulationComponent>(entity)
               && (!TryComp<MobStateComponent>(entity, out var mobstate) || mobstate.CurrentState == MobState.Alive);
    }

    public void SendTelepathicChat(EntityUid source, string message, bool hideChat)
    {
        if (!IsEligibleForTelepathy(source))
            return;

        var clients = GetPsionicChatClients().ToArray();
        var admins = GetAdminClients().ToArray();
        string messageWrap;
        string adminMessageWrap;



        if (!TryComp<PsionicComponent>(source, out var psionicComponent))
        {
            return;
        }

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

        if (_random.Prob(0.1f))
            _glimmerSystem.Glimmer++;

        if (_random.Prob(Math.Min(0.33f + ((float) _glimmerSystem.Glimmer / 1500), 1)))
        {
            float obfuscation = (0.25f + (float) _glimmerSystem.Glimmer / 2000);
            var obfuscated = _chatSystem.ObfuscateMessageReadability(message, obfuscation);
            _chatManager.ChatMessageToMany(ChatChannel.Telepathic, obfuscated, messageWrap, source, hideChat, false, GetDreamers(clients), Color.PaleVioletRed);
        }

        foreach (var repeater in EntityQuery<TelepathicRepeaterComponent>())
        {
            _chatSystem.TrySendInGameICMessage(repeater.Owner, message, InGameICChatType.Speak, false);
        }
    }
}
