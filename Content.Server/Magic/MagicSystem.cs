using Content.Server.Chat.Systems;
using Content.Shared.Backmen.Chat;
using Content.Shared.Magic;
using Content.Shared.Magic.Events;

namespace Content.Server.Magic;

public sealed class MagicSystem : SharedMagicSystem
{
    [Dependency] private readonly ChatSystem _chat = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SpeakSpellEvent>(OnSpellSpoken);
    }

    private void OnSpellSpoken(ref SpeakSpellEvent args)
    {
        // // start-backmen: magick
        // var ev = new Shared.Backmen.Magic.Events.CanUseMagicEvent
        // {
        //     User = args.User,
        // };
        // if(ev.Cancelled)
        //     return;
        // // end-backmen: magick
        _chat.TrySendInGameICMessage(args.Performer, Loc.GetString(args.Speech), InGameICChatType.Speak, false);
    }
}
