using Content.Server.Atmos.Components;
using Content.Server.Body.Systems;
using Content.Shared.Atmos;
using Content.Shared.Backmen.Body.Components;
using Content.Shared.StatusEffectNew;
using Content.Shared.StatusEffectNew.Components;

namespace Content.Server.Backmen.Body.Systems;

/// <summary>
/// Sole relay for inhale/exhale events into active status effects on a body.
/// </summary>
public sealed partial class StatusEffectBreathingRelaySystem : EntitySystem
{
    [Dependency] private StatusEffectsSystem _statusEffects = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StatusEffectContainerComponent, InhaleLocationEvent>(
            OnRelayInhaleLocation,
            after: [typeof(InternalsSystem)]);

        SubscribeLocalEvent<StatusEffectContainerComponent, ExhaleLocationEvent>(OnRelayExhaleLocation);
    }

    private void OnRelayInhaleLocation(Entity<StatusEffectContainerComponent> ent, ref InhaleLocationEvent args)
    {
        // backmen: space-animal-organs
        _statusEffects.RelayEvent(ent, ref args);
    }

    private void OnRelayExhaleLocation(Entity<StatusEffectContainerComponent> ent, ref ExhaleLocationEvent args)
    {
        // backmen: space-animal-organs
        _statusEffects.RelayEvent(ent, ref args);
    }
}
