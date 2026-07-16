using Content.Shared.Backmen.Surgery.Consciousness.Components;
using Content.Shared.Backmen.Targeting;

namespace Content.Shared.Damage.Systems;

public sealed partial class DamageableSystem
{
    [Dependency] private SharedTargetingSystem _targeting = default!;

    // start-backmen: combat-targeting
    private TargetBodyPart? ResolveCombatTargetBodyPart(
        EntityUid victim,
        EntityUid? origin,
        EntityUid? seedEntity)
    {
        if (!HasComp<ConsciousnessComponent>(victim))
            return null;

        if (origin == null && seedEntity == null)
            return null;

        if (!_targeting.TryResolveCombatBodyPart(victim, origin, seedEntity, out var hitPart))
            return null;

        return hitPart;
    }
    // end-backmen: combat-targeting
}
