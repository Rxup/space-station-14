using System.Text.RegularExpressions;
using Content.Server.Speech.Components;
using Content.Shared.Speech;
using Content.Shared.Speech.EntitySystems;

namespace Content.Server.Speech.EntitySystems;

// start-backmen: relay-accents
public sealed partial class BleatingAccentSystem : RelayAccentSystem<BleatingAccentComponent>
{
    private static readonly Regex BleatRegex = new("([mbdlpwhrkcnytfo])([aiu])", RegexOptions.IgnoreCase);

    public static string Accentuate(string message)
    {
        // Repeats the vowel in certain consonant-vowel pairs
        // So you taaaalk liiiike thiiiis
        return BleatRegex.Replace(message, "$1$2$2$2$2");
    }

    protected override string AccentuateInternal(EntityUid uid, BleatingAccentComponent comp, string message)
    {
        return Accentuate(message);
    }
}
// end-backmen: relay-accents
