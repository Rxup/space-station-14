using System.Text;
using System.Text.RegularExpressions;
using Content.Shared.Speech.Components;

namespace Content.Shared.Speech.EntitySystems;

public abstract class SharedRatvarianLanguageSystem : RelayAccentSystem<RatvarianLanguageComponent>
{
    public virtual void DoRatvarian(EntityUid uid, TimeSpan time, bool refresh = true)
    {
    }

    protected override string AccentuateInternal(EntityUid uid, RatvarianLanguageComponent comp, string message)
    {
        return Translate(message);
    }

    // This is the word of Ratvar and those who speak it shall abide by His rules:
    /*
     * Any time the word "of" occurs, it's linked to the previous word by a hyphen: "I am-of Ratvar"
     * Any time "th", followed by any two letters occurs, you add a grave (`) between those two letters: "Thi`s"
     * In the same vein, any time "ti" followed by one letter occurs, you add a grave (`) between "i" and the letter: "Ti`me"
     * Wherever "te" or "et" appear and there is another letter next to the "e", add a hyphen between "e" and the letter: "M-etal/Greate-r"
     * Where "gua" appears, add a hyphen between "gu" and "a": "Gu-ard"
     * Where the word "and" appears it's linked to all surrounding words by hyphens: "Sword-and-shield"
     * Where the word "to" appears, it's linked to the following word by a hyphen: "to-use"
     * Where the word "my" appears, it's linked to the following word by a hyphen: "my-light"
     * Any Ratvarian proper noun is not translated: Ratvar, Nezbere, Sevtug, Nzcrentr and Inath-neq
     *   (This only applies if they're being used as a proper noun: armorer/Nezbere)
     *
     * Russian mirrors of the same rules use matching conjunctions/prepositions and Cyrillic digraphs,
     * with a half-alphabet ROT on Cyrillic letters instead of Latin ROT13.
     */

    private const RegexOptions RuOpts = RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

    // English patterns
    private static readonly Regex THPattern = new(@"th\w\B", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex ETPattern = new(@"\Bet", RegexOptions.Compiled);
    private static readonly Regex TEPattern = new(@"te\B", RegexOptions.Compiled);
    private static readonly Regex OFPattern = new(@"(\s)(of)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex TIPattern = new(@"ti\B", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex GUAPattern = new(@"(gu)(a)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex ANDPattern = new(@"\b(\s)(and)(\s)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex TOMYPattern = new(@"\b(to|my)\s", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Russian patterns (applied alongside English so mixed messages work)
    private static readonly Regex THPatternRu = new(@"т[сшщх]\w\B", RuOpts); // ≈ th-cluster
    private static readonly Regex ETPatternRu = new(@"\Bет", RuOpts);
    private static readonly Regex TEPatternRu = new(@"те\B", RuOpts);
    private static readonly Regex OFPatternRu = new(@"(\s)(из|от|для)\b", RuOpts);
    private static readonly Regex TIPatternRu = new(@"ти\B", RuOpts);
    private static readonly Regex GUAPatternRu = new(@"(гу)(а)", RuOpts);
    private static readonly Regex ANDPatternRu = new(@"\b(\s)(и)(\s)", RuOpts);
    private static readonly Regex TOMYPatternRu = new(@"\b(к|ко|мой|моя|моё|мое|мои)\s", RuOpts);

    private static readonly Regex ProperNouns = new(
        @"(ratvar)|(nezbere)|(sevtuq)|(nzcrentr)|(inath-neq)|(ратвар)|(незбере)|(севтуг)|(нзкрентр)|(инат[-\s]?нек)",
        RuOpts);

    private const string CyrillicLower = "абвгдеёжзийклмнопрстуфхцчшщъыьэюя";
    private const string CyrillicUpper = "АБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯ";
    private const int CyrillicRot = 16; // half of 33-letter alphabet

    public static string Translate(string message)
    {
        var ruleTranslation = message;
        var finalMessage = new StringBuilder();
        var newWord = new StringBuilder();

        // English stylistic rules
        ruleTranslation = THPattern.Replace(ruleTranslation, "$&`");
        ruleTranslation = TEPattern.Replace(ruleTranslation, "$&-");
        ruleTranslation = ETPattern.Replace(ruleTranslation, "-$&");
        ruleTranslation = OFPattern.Replace(ruleTranslation, "-$2");
        ruleTranslation = TIPattern.Replace(ruleTranslation, "$&`");
        ruleTranslation = GUAPattern.Replace(ruleTranslation, "$1-$2");
        ruleTranslation = ANDPattern.Replace(ruleTranslation, "-$2-");
        ruleTranslation = TOMYPattern.Replace(ruleTranslation, "$1-");

        // Russian stylistic rules
        ruleTranslation = THPatternRu.Replace(ruleTranslation, "$&`");
        ruleTranslation = TEPatternRu.Replace(ruleTranslation, "$&-");
        ruleTranslation = ETPatternRu.Replace(ruleTranslation, "-$&");
        ruleTranslation = OFPatternRu.Replace(ruleTranslation, "-$2");
        ruleTranslation = TIPatternRu.Replace(ruleTranslation, "$&`");
        ruleTranslation = GUAPatternRu.Replace(ruleTranslation, "$1-$2");
        ruleTranslation = ANDPatternRu.Replace(ruleTranslation, "-$2-");
        ruleTranslation = TOMYPatternRu.Replace(ruleTranslation, "$1-");

        foreach (var word in ruleTranslation.Split(' '))
        {
            newWord.Clear();

            if (ProperNouns.IsMatch(word))
            {
                newWord.Append(word);
            }
            else
            {
                foreach (var letter in word)
                {
                    newWord.Append(RotLetter(letter));
                }
            }

            finalMessage.Append(newWord);
            finalMessage.Append(' ');
        }

        return finalMessage.ToString().Trim();
    }

    private static char RotLetter(char letter)
    {
        if (letter is >= 'a' and <= 'z')
        {
            var letterRot = letter + 13;
            if (letterRot > 'z')
                letterRot -= 26;
            return (char)letterRot;
        }

        if (letter is >= 'A' and <= 'Z')
        {
            var letterRot = letter + 13;
            if (letterRot > 'Z')
                letterRot -= 26;
            return (char)letterRot;
        }

        var lowerIdx = CyrillicLower.IndexOf(letter);
        if (lowerIdx >= 0)
            return CyrillicLower[(lowerIdx + CyrillicRot) % CyrillicLower.Length];

        var upperIdx = CyrillicUpper.IndexOf(letter);
        if (upperIdx >= 0)
            return CyrillicUpper[(upperIdx + CyrillicRot) % CyrillicUpper.Length];

        return letter;
    }
}
