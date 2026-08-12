using Content.Server.Chat.Systems;
using Content.Shared.ActionBlocker;
using Content.Shared.Actions.Events;
using Content.Shared.Chat;
using Content.Shared.Speech;
using Content.Shared.Speech.Components;
using Content.Shared.Speech.EntitySystems;

namespace Content.Server.Speech.EntitySystems;

/// <summary>
/// As soon as the chat refactor moves to Shared
/// the logic here can move to the shared <see cref="SharedSpeakOnActionSystem"/>
/// </summary>
public sealed partial class SpeakOnActionSystem : SharedSpeakOnActionSystem
{
    [Dependency] private ActionBlockerSystem _actionBlocker = default!; // backmen: muted-status-effects
    [Dependency] private ChatSystem _chat = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SpeakOnActionComponent, ActionPerformedEvent>(OnActionPerformed);
    }

    private void OnActionPerformed(Entity<SpeakOnActionComponent> ent, ref ActionPerformedEvent args)
    {
        var user = args.Performer;

        // If we can't speak, we can't speak
        // start-backmen: muted-status-effects
        if (!HasComp<SpeechComponent>(user) || !_actionBlocker.CanSpeak(user))
            return;
        // end-backmen: muted-status-effects

        if (string.IsNullOrWhiteSpace(ent.Comp.Sentence))
            return;

        _chat.TrySendInGameICMessage(user, Loc.GetString(ent.Comp.Sentence), InGameICChatType.Speak, false);
    }
}
