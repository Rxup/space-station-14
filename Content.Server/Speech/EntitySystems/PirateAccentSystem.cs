using System.Linq;
using Content.Server.Speech.Components;
using Content.Shared.Speech;
using Content.Shared.Speech.EntitySystems;
using Robust.Shared.Random;
using System.Text.RegularExpressions;

namespace Content.Server.Speech.EntitySystems;

// start-backmen: relay-accents
public sealed partial class PirateAccentSystem : RelayAccentSystem<PirateAccentComponent>
{
    private static readonly Regex FirstWordAllCapsRegex = new(@"^(\S+)");

    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private ReplacementAccentSystem _replacement = default!;

    // converts left word when typed into the right word. For example typing you becomes ye.
    public string Accentuate(string message, PirateAccentComponent component)
    {
        var msg = _replacement.ApplyReplacements(message, "pirate");

        if (!_random.Prob(component.YarrChance))
            return msg;
        //Checks if the first word of the sentence is all caps
        //So the prefix can be allcapped and to not resanitize the captial
        var firstWordAllCaps = !FirstWordAllCapsRegex.Match(msg).Value.Any(char.IsLower);

        var pick = _random.Pick(component.PirateWords);
        var pirateWord = Loc.GetString(pick);
        // Reverse sanitize capital
        if (!firstWordAllCaps)
            msg = msg[0].ToString().ToLower() + msg.Remove(0, 1);
        else
            pirateWord = pirateWord.ToUpper();
        msg = pirateWord + " " + msg;

        return msg;
    }

    protected override string AccentuateInternal(EntityUid uid, PirateAccentComponent comp, string message)
    {
        return Accentuate(message, comp);
    }
}
// end-backmen: relay-accents
