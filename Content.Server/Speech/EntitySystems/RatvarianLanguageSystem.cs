using Content.Shared.Speech.EntitySystems;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Prototypes;

namespace Content.Server.Speech.EntitySystems;

public sealed partial class RatvarianLanguageSystem : SharedRatvarianLanguageSystem
{
    [Dependency] private StatusEffectsSystem _statusEffects = default!;

    private static readonly EntProtoId RatvarianKey = "StatusEffectRatvarianLanguage";

    public override void DoRatvarian(EntityUid uid, TimeSpan time, bool refresh = true)
    {
        if (refresh)
            _statusEffects.TryAddStatusEffectDuration(uid, RatvarianKey, time);
        else
            _statusEffects.TrySetStatusEffectDuration(uid, RatvarianKey, time);
    }
}
