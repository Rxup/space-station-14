using System.Text;
using Content.Server.Speech.Components;
using Content.Shared.Speech;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Random;

namespace Content.Server.Speech.EntitySystems;

public sealed class MonkeyAccentSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<MonkeyAccentComponent, AccentGetEvent>(OnAccent);
        SubscribeLocalEvent<MonkeyAccentComponent, StatusEffectRelayedEvent<AccentGetEvent>>(OnAccentRelayed);
    }

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

    private void OnAccent(Entity<MonkeyAccentComponent> ent, ref AccentGetEvent args)
    {
        args.Message = Accentuate(args.Message);
    }

    private void OnAccentRelayed(Entity<MonkeyAccentComponent> ent, ref StatusEffectRelayedEvent<AccentGetEvent> args)
    {
        args.Args.Message = Accentuate(args.Args.Message);
    }
}
