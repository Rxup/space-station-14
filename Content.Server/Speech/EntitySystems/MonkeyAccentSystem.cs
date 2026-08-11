using System.Text;
using Content.Server.Speech.Components;
using Content.Shared.Speech;
using Content.Shared.Speech.EntitySystems;
using Robust.Shared.Random;

namespace Content.Server.Speech.EntitySystems;

// start-backmen: relay-accents
public sealed partial class MonkeyAccentSystem : RelayAccentSystem<MonkeyAccentComponent>
{
    [Dependency] private IRobustRandom _random = default!;

    public string Accentuate(string message)
    {
        var words = message.Split();
        var accentedMessage = new StringBuilder(message.Length + 2);

        for (var i = 0; i < words.Length; i++)
        {
            var word = words[i];

            if (_random.NextDouble() >= 0.5)
            {
                if (word.Length > 1)
                {
                    foreach (var _ in word)
                    {
                        accentedMessage.Append('У');  // Corvax-Localization
                    }

                    if (_random.NextDouble() >= 0.3)
                        accentedMessage.Append('К');  // Corvax-Localization
                }
                else
                    accentedMessage.Append('У');  // Corvax-Localization
            }
            else
            {
                foreach (var _ in word)
                {
                    if (_random.NextDouble() >= 0.8)
                        accentedMessage.Append('Г');  // Corvax-Localization
                    else
                        accentedMessage.Append('А');  // Corvax-Localization
                }

            }

            if (i < words.Length - 1)
                accentedMessage.Append(' ');
        }

        accentedMessage.Append('!');

        return accentedMessage.ToString();
    }

    protected override string AccentuateInternal(EntityUid uid, MonkeyAccentComponent comp, string message)
    {
        return Accentuate(message);
    }
}
// end-backmen: relay-accents
