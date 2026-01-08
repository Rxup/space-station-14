using System.Text.RegularExpressions;
using Content.Shared.Chat;
using Content.Shared.Speech;
using Robust.Shared.Prototypes;

namespace Content.Server.Speech;

public sealed class AccentSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!; // backmen
    public static readonly Regex SentenceRegex = new(@"(?<=[\.!\?‽])(?![\.!\?‽])", RegexOptions.Compiled);

    public override void Initialize()
    {
        SubscribeLocalEvent<TransformSpeechEvent>(AccentHandler);
    }

    private void AccentHandler(TransformSpeechEvent args)
    {
        var accentEvent = new AccentGetEvent(args.Sender, args.Message);

        RaiseLocalEvent(args.Sender, accentEvent, true);

        // start-backmen: language
        if (
            accentEvent.LanguageOverride is { } replaceLanguage &&
            _prototypeManager.TryIndex(replaceLanguage, out var replace))
        {
            args.Language = replace;
        }
        // end-backmen: language

        args.Message = accentEvent.Message;
    }
}
