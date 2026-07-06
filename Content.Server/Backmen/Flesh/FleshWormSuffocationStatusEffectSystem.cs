using Content.Server.Body.Systems;
using Content.Shared.Atmos;
using Content.Shared.Backmen.Flesh;
using Content.Shared.StatusEffectNew;

namespace Content.Server.Backmen.Flesh;

public sealed partial class FleshWormSuffocationStatusEffectSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FleshWormSuffocationStatusEffectComponent, StatusEffectRelayedEvent<InhaleLocationEvent>>(
            OnInhale);
    }

    private void OnInhale(
        Entity<FleshWormSuffocationStatusEffectComponent> ent,
        ref StatusEffectRelayedEvent<InhaleLocationEvent> args)
    {
        args.Args = args.Args with { Gas = GasMixture.SpaceGas };
    }
}
