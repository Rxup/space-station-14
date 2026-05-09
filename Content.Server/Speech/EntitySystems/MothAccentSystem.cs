using System.Text.RegularExpressions;
using Content.Server.Speech.Components;
using Robust.Shared.Random;
using Content.Shared.Speech;
using Content.Shared.StatusEffectNew;

namespace Content.Server.Speech.EntitySystems;

public sealed partial class MothAccentSystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = default!; // Corvax-Localization

    // Corvax-Localization-Start
    private static readonly Regex RegexLoc1_1 = new("ж{1,3}");
    private static readonly Regex RegexLoc1_2 = new("Ж{1,3}");

    private static readonly Regex RegexLoc2_1 = new("з{1,3}");
    private static readonly Regex RegexLoc2_2 = new("З{1,3}");
    // Corvax-Localization-End

    private static readonly Regex RegexLowerBuzz = new Regex("z{1,3}");
    private static readonly Regex RegexUpperBuzz = new Regex("Z{1,3}");

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MothAccentComponent, AccentGetEvent>(OnAccent);
        SubscribeLocalEvent<MothAccentComponent, StatusEffectRelayedEvent<AccentGetEvent>>(OnAccentRelayed);
    }

    private void OnAccentRelayed(Entity<MothAccentComponent> ent, ref StatusEffectRelayedEvent<AccentGetEvent> args)
    {
        args.Args.Message = Accentuate(args.Args.Message);
    }

    private void OnAccent(Entity<MothAccentComponent> ent, ref AccentGetEvent args)
    {
        args.Message = Accentuate(args.Message);
    }

    public string Accentuate(string message)
    {
        // buzzz
        message = RegexLowerBuzz.Replace(message, "zzz");
        // buZZZ
        message = RegexUpperBuzz.Replace(message, "ZZZ");

        // Corvax-Localization-Start
        // ж => жжж
        message = RegexLoc1_1.Replace(
            message,
            _=> _random.Pick(new List<string>() { "жж", "жжж" })
        );
        // Ж => ЖЖЖ
        message = RegexLoc1_2.Replace(
            message,
            _=> _random.Pick(new List<string>() { "ЖЖ", "ЖЖЖ" })
        );
        // з => ссс
        message = RegexLoc2_1.Replace(
            message,
            _=> _random.Pick(new List<string>() { "зз", "ззз" })
        );
        // З => CCC
        message = RegexLoc2_2.Replace(
            message,
            _=> _random.Pick(new List<string>() { "ЗЗ", "ЗЗЗ" })
        );
        // Corvax-Localization-End

        return message;
    }
}
