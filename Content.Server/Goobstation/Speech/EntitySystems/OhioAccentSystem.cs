using Content.Server.Speech.Components;
using Content.Shared.Speech;
using Content.Shared.Speech.EntitySystems;
using Robust.Shared.Random;

namespace Content.Server.Speech.EntitySystems;

// start-backmen: relay-accents
public sealed partial class OhioAccentSystem : RelayAccentSystem<OhioAccentComponent>
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private ReplacementAccentSystem _replacement = default!;

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

    protected override string AccentuateInternal(EntityUid uid, OhioAccentComponent comp, string message)
    {
        return Accentuate(message);
    }
};
// end-backmen: relay-accents
