using System.Text.RegularExpressions;
using Content.Server.Backmen.Speech.Components;
using Content.Server.Speech;
using Content.Shared.Speech;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Random;

namespace Content.Server.Backmen.Speech.EntitySystems;

public sealed partial class RoarAccentSystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = default!;

    private static readonly Regex R1 = new(@"r+", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex R2 = new(@"R+", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex R3 = new(@"р+", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly List<string> R3R = ["рр", "ррр"];

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RoarAccentComponent, AccentGetEvent>(OnAccent);
        SubscribeLocalEvent<RoarAccentComponent, StatusEffectRelayedEvent<AccentGetEvent>>(OnAccentRelayed);
    }

    private void OnAccentRelayed(Entity<RoarAccentComponent> ent, ref StatusEffectRelayedEvent<AccentGetEvent> args)
    {
        args.Args.Message = Accentuate(args.Args.Message);
    }

    private void OnAccent(Entity<RoarAccentComponent> ent, ref AccentGetEvent args)
    {
        args.Message = Accentuate(args.Message);
    }

    public string Accentuate(string message)
    {
        // roarrr
        message = R1.Replace(message, "rrr");
        // roarRR
        message = R2.Replace(message, "RRR");
        // ADT-Localization-Start
        // р => ррр
        message = R3.Replace(message, _=>_random.Pick(R3R));
        // ADT-Localization-End

        return message;
    }
}
