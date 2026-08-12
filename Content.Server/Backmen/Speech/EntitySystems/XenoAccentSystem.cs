using System.Text;
using Content.Server.Backmen.Speech.Components;
using Content.Shared.Speech;
using Content.Shared.Speech.EntitySystems;
using Robust.Shared.Random;

namespace Content.Server.Backmen.Speech.EntitySystems;

public sealed partial class XenoAccentSystem : RelayAccentSystem<XenoAccentComponent>
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
                accentedMessage.Append("ХИ");
                if (word.Length > 1)
                {
                    foreach (var _ in word)
                    {
                        accentedMessage.Append('С');
                    }

                    if (_random.NextDouble() >= 0.3)
                        accentedMessage.Append('с');
                }
                else
                    accentedMessage.Append('С');
            }
            else
            {
                accentedMessage.Append("ХИ");
                foreach (var _ in word)
                {
                    if (_random.NextDouble() >= 0.8)
                        accentedMessage.Append('Х');
                    else
                        accentedMessage.Append('С');
                }

            }

            if (i < words.Length - 1)
                accentedMessage.Append(' ');
        }

        accentedMessage.Append('!');

        return accentedMessage.ToString();
    }

    protected override string AccentuateInternal(EntityUid uid, XenoAccentComponent comp, string message)
    {
        return Accentuate(message);
    }
}
