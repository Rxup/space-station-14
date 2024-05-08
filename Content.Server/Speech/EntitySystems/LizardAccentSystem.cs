using System.Text.RegularExpressions;
using Content.Server.Speech.Components;
using Robust.Shared.Random;

namespace Content.Server.Speech.EntitySystems;

public sealed class LizardAccentSystem : EntitySystem
{
    private static readonly Regex RegexLowerS = new("s+");
    private static readonly Regex RegexUpperS = new("S+");
    private static readonly Regex RegexInternalX = new(@"(\w)x");
    private static readonly Regex RegexLowerEndX = new(@"\bx([\-|r|R]|\b)");
    private static readonly Regex RegexUpperEndX = new(@"\bX([\-|r|R]|\b)");

    // Corvax-Localization-Start
    private static readonly Regex RegexLoc1_1 = new("с+");
    private static readonly Regex RegexLoc1_2 = new("С+");

    private static readonly Regex RegexLoc2_1 = new("з+");
    private static readonly Regex RegexLoc2_2 = new("З+");

    private static readonly Regex RegexLoc3_1 = new("ш+");
    private static readonly Regex RegexLoc3_2 = new("Ш+");

    private static readonly Regex RegexLoc4_1 = new("ч+");
    private static readonly Regex RegexLoc4_2 = new("Ч+");
    // Corvax-Localization-End
    [Dependency] private readonly IRobustRandom _random = default!; // Corvax-Localization

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LizardAccentComponent, AccentGetEvent>(OnAccent);
    }

    private void OnAccent(EntityUid uid, LizardAccentComponent component, AccentGetEvent args)
    {
        var message = args.Message;

        // hissss
        message = RegexLowerS.Replace(message, "sss");
        // hiSSS
        message = RegexUpperS.Replace(message, "SSS");
        // ekssit
        message = RegexInternalX.Replace(message, "$1kss");
        // ecks
        message = RegexLowerEndX.Replace(message, "ecks$1");
        // eckS
        message = RegexUpperEndX.Replace(message, "ECKS$1");

        // Corvax-Localization-Start
        // c => ссс
        message = RegexLoc1_1.Replace(
            message,
            _=> _random.Pick(new List<string>() { "сс", "ссс" })
        );
        // С => CCC
        message = RegexLoc1_2.Replace(
            message,
            _=> _random.Pick(new List<string>() { "СС", "ССС" })
        );
        // з => ссс
        message = RegexLoc2_1.Replace(
            message,
            _=> _random.Pick(new List<string>() { "сс", "ссс" })
        );
        // З => CCC
        message = RegexLoc2_2.Replace(
            message,
            _=> _random.Pick(new List<string>() { "СС", "ССС" })
        );
        // ш => шшш
        message = RegexLoc3_1.Replace(
            message,
            _=> _random.Pick(new List<string>() { "шш", "шшш" })
        );
        // Ш => ШШШ
        message = RegexLoc3_2.Replace(
            message,
            _=> _random.Pick(new List<string>() { "ШШ", "ШШШ" })
        );
        // ч => щщщ
        message = RegexLoc4_1.Replace(
            message,
            _=> _random.Pick(new List<string>() { "щщ", "щщщ" })
        );
        // Ч => ЩЩЩ
        message = RegexLoc1_2.Replace(
            message,
            _=> _random.Pick(new List<string>() { "ЩЩ", "ЩЩЩ" })
        );
        // Corvax-Localization-End
        args.Message = message;
    }
}
