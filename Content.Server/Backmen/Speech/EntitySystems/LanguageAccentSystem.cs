using Content.Server.Backmen.Speech.Components;
using Content.Shared.Speech;
using Content.Shared.Speech.EntitySystems;
using Robust.Shared.Random;

namespace Content.Server.Backmen.Speech.EntitySystems;

/// <summary>
/// Randomly overrides the spoken language when <see cref="LanguageAccentComponent"/> is present
/// (including when applied via status effects).
/// </summary>
public sealed partial class LanguageAccentSystem : RelayAccentSystem<LanguageAccentComponent>
{
    [Dependency] private IRobustRandom _random = default!;

    protected override string AccentuateInternal(EntityUid uid, LanguageAccentComponent comp, string message)
    {
        return message;
    }

    protected override void ApplyAccent(EntityUid speaker, LanguageAccentComponent comp, AccentGetEvent args)
    {
        if (!_random.Prob(comp.Chance))
            return;

        args.LanguageOverride = comp.Language;
    }
}
