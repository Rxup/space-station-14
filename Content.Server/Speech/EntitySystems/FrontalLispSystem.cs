using System.Text.RegularExpressions;
using Content.Server.Speech.Components;
using Robust.Shared.Random; // Corvax-Localization

namespace Content.Server.Speech.EntitySystems;

public sealed class FrontalLispSystem : EntitySystem
{
    // @formatter:off
    private static readonly Regex RegexUpperTh = new(@"[T]+[Ss]+|[S]+[Cc]+(?=[IiEeYy]+)|[C]+(?=[IiEeYy]+)|[P][Ss]+|([S]+[Tt]+|[T]+)(?=[Ii]+[Oo]+[Uu]*[Nn]*)|[C]+[Hh]+(?=[Ii]*[Ee]*)|[Z]+|[S]+|[X]+(?=[Ee]+)");
    private static readonly Regex RegexLowerTh = new(@"[t]+[s]+|[s]+[c]+(?=[iey]+)|[c]+(?=[iey]+)|[p][s]+|([s]+[t]+|[t]+)(?=[i]+[o]+[u]*[n]*)|[c]+[h]+(?=[i]*[e]*)|[z]+|[s]+|[x]+(?=[e]+)");
    private static readonly Regex RegexUpperEcks = new(@"[E]+[Xx]+[Cc]*|[X]+");
    private static readonly Regex RegexLowerEcks = new(@"[e]+[x]+[c]*|[x]+");
    // Corvax-Localization Start
    private static readonly Regex RegexLoc1_1 = new(@"с");
    private static readonly Regex RegexLoc1_2 = new(@"С");
    private static readonly Regex RegexLoc2_1 = new(@"ч");
    private static readonly Regex RegexLoc2_2 = new(@"Ч");
    private static readonly Regex RegexLoc3_1 = new(@"ц");
    private static readonly Regex RegexLoc3_2 = new(@"Ц");
    private static readonly Regex RegexLoc4_1 = new(@"\B[т](?![АЕЁИОУЫЭЮЯаеёиоуыэюя])");
    private static readonly Regex RegexLoc4_2 = new(@"\B[Т](?![АЕЁИОУЫЭЮЯаеёиоуыэюя])");
    private static readonly Regex RegexLoc5_1 = new(@"з");
    private static readonly Regex RegexLoc5_2 = new(@"З");

    // Corvax-Localization End
    // @formatter:on

    [Dependency] private readonly IRobustRandom _random = default!; // Corvax-Localization

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FrontalLispComponent, AccentGetEvent>(OnAccent);
    }

    private void OnAccent(EntityUid uid, FrontalLispComponent component, AccentGetEvent args)
    {
        var message = args.Message;

        // handles ts, sc(i|e|y), c(i|e|y), ps, st(io(u|n)), ch(i|e), z, s
        message = RegexUpperTh.Replace(message, "TH");
        message = RegexLowerTh.Replace(message, "th");
        // handles ex(c), x
        message = RegexUpperEcks.Replace(message, "EKTH");
        message = RegexLowerEcks.Replace(message, "ekth");
        // Corvax-Localization Start
        // с - ш
        message = RegexLoc1_1.Replace(message, _=> _random.Prob(0.90f) ? "ш" : "с");
        message = RegexLoc1_2.Replace(message, _=> _random.Prob(0.90f) ? "Ш" : "С");
        // ч - ш
        message = RegexLoc2_1.Replace(message, _=> _random.Prob(0.90f) ? "ш" : "ч");
        message = RegexLoc2_2.Replace(message, _=> _random.Prob(0.90f) ? "Ш" : "Ч");
        // ц - ч
        message = RegexLoc3_1.Replace(message, _=> _random.Prob(0.90f) ? "ч" : "ц");
        message = RegexLoc3_2.Replace(message, _=> _random.Prob(0.90f) ? "Ч" : "Ц");
        // т - ч
        message = RegexLoc4_1.Replace(message,  _=>  _random.Prob(0.90f) ? "ч" : "т");
        message = RegexLoc4_2.Replace(message,  _=>  _random.Prob(0.90f) ? "Ч" : "Т");
        // з - ж
        message = RegexLoc5_1.Replace(message, _=>  _random.Prob(0.90f) ? "ж" : "з");
        message = RegexLoc5_2.Replace(message,  _=> _random.Prob(0.90f) ? "Ж" : "З");
        // Corvax-Localization End
        args.Message = message;
    }
}
