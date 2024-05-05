using System.Text.RegularExpressions;
using Content.Server.Speech.Components;
using Robust.Shared.Random; // Corvax-Localization

namespace Content.Server.Speech.EntitySystems;

public sealed class FrontalLispSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!; // Corvax-Localization
    // @formatter:off
    private static readonly Regex RegexUpperTh = new(@"[T]+[Ss]+|[S]+[Cc]+(?=[IiEeYy]+)|[C]+(?=[IiEeYy]+)|[P][Ss]+|([S]+[Tt]+|[T]+)(?=[Ii]+[Oo]+[Uu]*[Nn]*)|[C]+[Hh]+(?=[Ii]*[Ee]*)|[Z]+|[S]+|[X]+(?=[Ee]+)");
    private static readonly Regex RegexLowerTh = new(@"[t]+[s]+|[s]+[c]+(?=[iey]+)|[c]+(?=[iey]+)|[p][s]+|([s]+[t]+|[t]+)(?=[i]+[o]+[u]*[n]*)|[c]+[h]+(?=[i]*[e]*)|[z]+|[s]+|[x]+(?=[e]+)");
    private static readonly Regex RegexUpperEcks = new(@"[E]+[Xx]+[Cc]*|[X]+");
    private static readonly Regex RegexLowerEcks = new(@"[e]+[x]+[c]*|[x]+");
    // Corvax-Localization Start
    private static readonly Regex RegexLoc1_1 = new(@"\B[СВЧТ]\B");
    private static readonly Regex RegexLoc1_2 = new(@"\B[свчт]\B");
    private static readonly Regex RegexLoc2_1 = new(@"\b[Д](?![ИЕЁЮЯЬ])\b|\B[Д]\B");
    private static readonly Regex RegexLoc2_2 = new(@"\b[Дд](?![ИиЕеЁёЮюЯяЬь])\b|\B[Дд]\B");

    // Corvax-Localization End
    // @formatter:on

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
        // с, в, ч, т in ф or ш
        message = RegexLoc1_1.Replace(message, _=> _random.Prob(0.5f) ? "Ф" : "Ш");
        message = RegexLoc1_2.Replace(message, _=> _random.Prob(0.5f) ? "ф" : "ш");
        // д in ф
        message = RegexLoc2_1.Replace(message, "Ф");
        message = RegexLoc2_2.Replace(message, "ф");
        // Corvax-Localization End
        args.Message = message;
    }
}
