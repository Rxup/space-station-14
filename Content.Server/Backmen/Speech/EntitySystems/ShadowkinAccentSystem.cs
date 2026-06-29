using System.Text;
using System.Text.RegularExpressions;
using Content.Server.Backmen.Speech.Components;
using Content.Shared.Speech;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Random;

namespace Content.Server.Backmen.Speech.EntitySystems;

public sealed partial class ShadowkinAccentSystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = default!;

    private static readonly Regex MRegex = new(@"[adgjmpsvy]", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex ARegex = new(@"[behknqtwz]", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex RRegex = new(@"[cfilorux]", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex MRegexRu = new(@"[бвгджзклмнпстфхцчшщ]",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex ARegexRu = new(@"[аеёиоуыэюя]", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex RRegexRu = new(@"[р]", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public override void Initialize()
    {
        SubscribeLocalEvent<ShadowkinAccentComponent, AccentGetEvent>(OnAccent);
        SubscribeLocalEvent<ShadowkinAccentComponent, StatusEffectRelayedEvent<AccentGetEvent>>(OnAccentRelayed);
    }

    private void OnAccentRelayed(Entity<ShadowkinAccentComponent> ent, ref StatusEffectRelayedEvent<AccentGetEvent> args)
    {
        args.Args.Message = Accentuate(args.Args.Message);
    }

    public string Accentuate(string message)
    {
        var result = new StringBuilder();
        foreach (var c in message)
        {
            var current = c.ToString();

            // Английские замены (шанс 10%)
            if (_random.Prob(0.1f) && MRegex.IsMatch(current))
                current = "m";
            if (_random.Prob(0.1f) && ARegex.IsMatch(current))
                current = "a";
            if (_random.Prob(0.1f) && RRegex.IsMatch(current))
                current = "r";

            // Русские замены (шанс 10%)
            if (_random.Prob(0.1f) && MRegexRu.IsMatch(current))
                current = "м";
            if (_random.Prob(0.1f) && ARegexRu.IsMatch(current))
                current = "а";
            if (_random.Prob(0.1f) && RRegexRu.IsMatch(current))
                current = "р";

            result.Append(current);
        }

        return result.ToString().Trim();
    }

    private void OnAccent(Entity<ShadowkinAccentComponent> ent, ref AccentGetEvent args)
    {
        args.Message = Accentuate(args.Message);
    }
}
