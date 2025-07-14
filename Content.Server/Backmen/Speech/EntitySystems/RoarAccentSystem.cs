using System.Text.RegularExpressions;
using Content.Server.Backmen.Speech.Components;
using Content.Server.Speech;
using Robust.Shared.Random;

namespace Content.Server.Backmen.Speech.EntitySystems;

public sealed class RoarAccentSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;

    private static readonly Regex R1 = new(@"r+", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex R2 = new(@"R+", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex R3 = new(@"р+", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly List<string> R3R = ["рр", "ррр"];

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RoarAccentComponent, AccentGetEvent>(OnAccent);
    }

    private void OnAccent(EntityUid uid, RoarAccentComponent component, AccentGetEvent args)
    {
        var message = args.Message;

        // roarrr
        message = R1.Replace(message, "rrr");
        // roarRR
        message = R2.Replace(message, "RRR");
        // ADT-Localization-Start
        // р => ррр
        message = R3.Replace(message, _=>_random.Pick(R3R));
        // ADT-Localization-End
        args.Message = message;
    }
}
