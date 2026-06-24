using Content.Server.Body.Systems;
using Content.Shared.Atmos;
using Content.Shared.Backmen.Flesh;
using Content.Shared.StatusEffectNew;
using Content.Shared.StatusEffectNew.Components;

namespace Content.Server.Backmen.Flesh;

public sealed class FleshWormSuffocationStatusEffectSystem : EntitySystem
{
    [Dependency] private StatusEffectsSystem _statusEffects = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StatusEffectContainerComponent, InhaleLocationEvent>(
            OnRelayInhaleLocation, after: [typeof(InternalsSystem)]);
        SubscribeLocalEvent<FleshWormSuffocationStatusEffectComponent, StatusEffectRelayedEvent<InhaleLocationEvent>>(
            OnInhale);
    }

    private void OnRelayInhaleLocation(Entity<StatusEffectContainerComponent> ent, ref InhaleLocationEvent args)
    {
        _statusEffects.RelayEvent(ent, ref args);
    }

    private void OnInhale(
        Entity<FleshWormSuffocationStatusEffectComponent> ent,
        ref StatusEffectRelayedEvent<InhaleLocationEvent> args)
    {
        args.Args = args.Args with { Gas = GasMixture.SpaceGas };
    }
}
