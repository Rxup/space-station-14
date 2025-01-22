using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Reactions;
using JetBrains.Annotations;

namespace Content.Server.Atmos.Reactions;

[UsedImplicitly]
public sealed partial class HalonProductionReaction : IGasReactionEffect
{
    public ReactionResult React(GasMixture mixture, IGasMixtureHolder? holder, AtmosphereSystem atmosphereSystem, float heatScale)
    {
        var initialHyperNoblium = mixture.GetMoles(Gas.HyperNoblium);
        if (initialHyperNoblium >= 5.0f && mixture.Temperature > 20f)
            return ReactionResult.NoReaction;

        var initialBZ = mixture.GetMoles(Gas.BZ);
        var initialTritium = mixture.GetMoles(Gas.Tritium);
        var initialHalon = mixture.GetMoles(Gas.Halon);

        var producedAmount = Math.Min(Atmospherics.HalonMaxRate, Math.Min(initialTritium * 0.5f, initialBZ * 0.2f));

        if (producedAmount <= 0 || initialBZ - producedAmount * 0.2f < 0 || initialTritium - producedAmount * 0.5f < 0 || initialBZ > initialTritium + initialHalon)
            return ReactionResult.NoReaction;

        var oldHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);

        mixture.AdjustMoles(Gas.BZ, -producedAmount * 0.2f);
        mixture.AdjustMoles(Gas.Tritium, -producedAmount * 0.5f);
        mixture.AdjustMoles(Gas.Halon, producedAmount * 0.4f);

        var energyReleased = producedAmount * Atmospherics.HalonFormationEnergy;
        var newHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);
        if (newHeatCapacity > Atmospherics.MinimumHeatCapacity)
            mixture.Temperature = Math.Max((mixture.Temperature * oldHeatCapacity + energyReleased) / newHeatCapacity, Atmospherics.TCMB);

        return ReactionResult.Reacting;
    }
}
