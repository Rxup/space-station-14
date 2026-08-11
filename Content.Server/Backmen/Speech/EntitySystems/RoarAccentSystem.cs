using System.Text.RegularExpressions;
using Content.Server.Backmen.Speech.Components;
using Content.Shared.Speech;
using Content.Shared.Speech.EntitySystems;
using Robust.Shared.Random;

namespace Content.Server.Backmen.Speech.EntitySystems;

public sealed partial class RoarAccentSystem : RelayAccentSystem<RoarAccentComponent>
{
    [Dependency] private IRobustRandom _random = default!;

    private static readonly Regex R1 = new(@"r+", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex R2 = new(@"R+", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex R3 = new(@"р+", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly List<string> R3R = ["рр", "ррр"];

    public string Accentuate(string message)
    {
        // roarrr
        message = R1.Replace(message, "rrr");
        // roarRR
        message = R2.Replace(message, "RRR");
        // ADT-Localization-Start
        // р => ррр
        message = R3.Replace(message, _=>_random.Pick(R3R));
        // ADT-Localization-End

        return message;
    }

    protected override string AccentuateInternal(EntityUid uid, RoarAccentComponent comp, string message)
    {
        return Accentuate(message);
    }
}
