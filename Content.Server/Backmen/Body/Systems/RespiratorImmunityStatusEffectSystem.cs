using Content.Server.Body.Systems;
using Content.Shared.Atmos;
using Content.Shared.Backmen.Body.Components;
using Content.Shared.StatusEffectNew;

namespace Content.Server.Backmen.Body.Systems;

public sealed class RespiratorImmunityStatusEffectSystem : EntitySystem
{
    private readonly GasMixture _spaceBreathAtmos;

    public RespiratorImmunityStatusEffectSystem()
    {
        _spaceBreathAtmos = new GasMixture(Atmospherics.CellVolume)
        {
            Temperature = Atmospherics.T20C
        };
        _spaceBreathAtmos.AdjustMoles(Gas.Oxygen, Atmospherics.OxygenMolesStandard);
        _spaceBreathAtmos.AdjustMoles(Gas.Nitrogen, Atmospherics.NitrogenMolesStandard);
        _spaceBreathAtmos.MarkImmutable();
    }

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RespiratorImmunityStatusEffectComponent, StatusEffectRelayedEvent<InhaleLocationEvent>>(
            OnInhale);
        SubscribeLocalEvent<RespiratorImmunityStatusEffectComponent, StatusEffectRelayedEvent<ExhaleLocationEvent>>(
            OnExhale);
    }

    private void OnInhale(
        Entity<RespiratorImmunityStatusEffectComponent> ent,
        ref StatusEffectRelayedEvent<InhaleLocationEvent> args)
    {
        args.Args = args.Args with { Gas = _spaceBreathAtmos };
    }

    private void OnExhale(
        Entity<RespiratorImmunityStatusEffectComponent> ent,
        ref StatusEffectRelayedEvent<ExhaleLocationEvent> args)
    {
        args.Args = args.Args with { Gas = GasMixture.SpaceGas };
    }
}
