using System.Text.RegularExpressions;
using Content.Server.Speech.Components;
using Content.Shared.Speech;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Random;

namespace Content.Server.Speech.EntitySystems;

public sealed class OhioAccentSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ReplacementAccentSystem _replacement = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<OhioAccentComponent, AccentGetEvent>(OnAccent);
        SubscribeLocalEvent<OhioAccentComponent, StatusEffectRelayedEvent<AccentGetEvent>>(OnAccentRelayed);
    }

    private void OnAccentRelayed(Entity<OhioAccentComponent> ent, ref StatusEffectRelayedEvent<AccentGetEvent> args)
    {
        args.Args.Message = Accentuate(args.Args.Message);
    }

    private void OnAccent(Entity<OhioAccentComponent> ent, ref AccentGetEvent args)
    {
        args.Message = Accentuate(args.Message);
    }

    public string Accentuate(string message)
    {
        message = _replacement.ApplyReplacements(message, "ohio");

        // Prefix
        if (_random.Prob(0.15f))
        {
            var pick = _random.Next(1, 7);

            // Reverse sanitize capital
            message = message[0].ToString().ToLower() + message.Remove(0, 1);
            message = Loc.GetString($"accent-ohio-prefix-{pick}") + " " + message;
        }

        // Sanitize capital again, in case we substituted a word that should be capitalized
        message = message[0].ToString().ToUpper() + message.Remove(0, 1);

        // Suffixes
        if (_random.Prob(0.3f))
        {
            var pick = _random.Next(1, 13);
            message += Loc.GetString($"accent-ohio-suffix-{pick}");
        }

        return message;
    }
};
