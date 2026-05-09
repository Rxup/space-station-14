using System.Text.RegularExpressions;
using Content.Server.Corvax.Speech.Components;
using Content.Server.Speech;
using Content.Shared.Speech;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Random;

namespace Content.Server.Corvax.Speech.EntitySystems;

public sealed partial class GrowlingAccentSystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = default!;
    private static readonly Regex LowerRRegex = new(@"r+", RegexOptions.Compiled);
    private static readonly Regex UpperRRegex = new(@"R+", RegexOptions.Compiled);
    private static readonly Regex LowerRuRRegex = new(@"р+", RegexOptions.Compiled);
    private static readonly Regex UpperRuRRegex = new(@"Р+", RegexOptions.Compiled);

    private static readonly List<string> LowerRReplacements = ["rr", "rrr"];
    private static readonly List<string> UpperRReplacements = ["RR", "RRR"];
    private static readonly List<string> LowerRuRReplacements = ["рр", "ррр"];
    private static readonly List<string> UpperRuRReplacements = ["РР", "РРР"];

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GrowlingAccentComponent, AccentGetEvent>(OnAccent);
        SubscribeLocalEvent<GrowlingAccentComponent, StatusEffectRelayedEvent<AccentGetEvent>>(OnAccentRelayed);
    }

    private void OnAccentRelayed(Entity<GrowlingAccentComponent> ent, ref StatusEffectRelayedEvent<AccentGetEvent> args)
    {
        args.Args.Message = Accentuate(args.Args.Message);
    }

    private void OnAccent(Entity<GrowlingAccentComponent> ent, ref AccentGetEvent args)
    {
        args.Message = Accentuate(args.Message);
    }

    public string Accentuate(string message)
    {
        // r => rrr
        message = LowerRRegex.Replace(message, _ => _random.Pick(LowerRReplacements));

        // R => RRR
        message = UpperRRegex.Replace(message, _ => _random.Pick(UpperRReplacements));

        // р => ррр
        message = LowerRuRRegex.Replace(message, _ => _random.Pick(LowerRuRReplacements));

        // Р => РРР
        message = UpperRuRRegex.Replace(message, _ => _random.Pick(UpperRuRReplacements));

        return message;
    }
}
