using System.Text;
using System.Text.RegularExpressions;
using Content.Server.Backmen.Speech.Components;
using Content.Server.Speech;
using Content.Server.Speech.Components;
using Content.Shared.Speech;
using Robust.Shared.Random;

namespace Content.Server.Backmen.Speech.EntitySystems;

public sealed class ShadowkinAccentSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;

    private static readonly Regex mRegex = new(@"[adgjmpsvy]", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex aRegex = new(@"[behknqtwz]", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex rRegex = new(@"[cfilorux]", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex mRegexRu = new(@"[бвгджзклмнпстфхцчшщ]",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex aRegexRu = new(@"[аеёиоуыэюя]", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex rRegexRu = new(@"[р]", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public override void Initialize()
    {
        SubscribeLocalEvent<ShadowkinAccentComponent, AccentGetEvent>(OnAccent);
    }

    public string Accentuate(string message)
    {
        var result = new StringBuilder();
        foreach (var c in message)
        {
            var current = c.ToString();

            // Английские замены (шанс 10%)
            if (_random.Prob(0.1f) && mRegex.IsMatch(current))
                current = "m";
            if (_random.Prob(0.1f) && aRegex.IsMatch(current))
                current = "a";
            if (_random.Prob(0.1f) && rRegex.IsMatch(current))
                current = "r";

            // Русские замены (шанс 10%)
            if (_random.Prob(0.1f) && mRegexRu.IsMatch(current))
                current = "м";
            if (_random.Prob(0.1f) && aRegexRu.IsMatch(current))
                current = "а";
            if (_random.Prob(0.1f) && rRegexRu.IsMatch(current))
                current = "р";

            result.Append(current);
        }

        return result.ToString().Trim();
    }

    private void OnAccent(EntityUid uid, ShadowkinAccentComponent component, AccentGetEvent args)
    {
        args.Message = Accentuate(args.Message);
    }
}
