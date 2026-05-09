using System.Text;
using Content.Server.Speech.Components;
using Content.Shared.Speech;
using Content.Shared.StatusEffectNew;

namespace Content.Server.Speech.EntitySystems;

public sealed partial class RussianAccentSystem : EntitySystem
{
    [Dependency] private ReplacementAccentSystem _replacement = default!;
    public override void Initialize()
    {
        SubscribeLocalEvent<RussianAccentComponent, AccentGetEvent>(OnAccent);
        SubscribeLocalEvent<RussianAccentComponent, StatusEffectRelayedEvent<AccentGetEvent>>(OnAccentRelayed);
    }

    private void OnAccentRelayed(Entity<RussianAccentComponent> ent, ref StatusEffectRelayedEvent<AccentGetEvent> args)
    {
        args.Args.Message = Accentuate(args.Args.Message);
    }

    public string Accentuate(string message)
    {
        var accentedMessage = new StringBuilder(_replacement.ApplyReplacements(message, "russian"));

        for (var i = 0; i < accentedMessage.Length; i++)
        {
            var c = accentedMessage[i];

            accentedMessage[i] = c switch
            {
                'A' => 'Д',
                'b' => 'в',
                'N' => 'И',
                'n' => 'и',
                'K' => 'К',
                'k' => 'к',
                'm' => 'м',
                'h' => 'н',
                't' => 'т',
                'R' => 'Я',
                'r' => 'я',
                'Y' => 'У',
                'W' => 'Ш',
                'w' => 'ш',
                _ => accentedMessage[i]
            };
        }

        return accentedMessage.ToString();
    }

    private void OnAccent(Entity<RussianAccentComponent> ent, ref AccentGetEvent args)
    {
        args.Message = Accentuate(args.Message);
    }
}
