using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Reactions;
using JetBrains.Annotations;

namespace Content.Server.Atmos.Reactions;

[UsedImplicitly]
public sealed partial class HalonFrezonDecompositionReaction : IGasReactionEffect
{
    public ReactionResult React(GasMixture mixture, IGasMixtureHolder? holder, AtmosphereSystem atmosphereSystem, float heatScale)
    {
        var initialHyperNoblium = mixture.GetMoles(Gas.HyperNoblium);
        if (initialHyperNoblium >= 5.0f && mixture.Temperature > 20f)
            return ReactionResult.NoReaction;

        var initialHalon = mixture.GetMoles(Gas.Halon);
        var initialFrezon = mixture.GetMoles(Gas.Frezon);

        var environmentEfficiency = mixture.Pressure * Atmospherics.HalonFrezonDecompositionPressureBonus / mixture.Volume;
        var ratioEfficiency = Math.Min(initialHalon / initialFrezon, 1);

        var producedAmount = Math.Min(ratioEfficiency * environmentEfficiency * 2f, Math.Min(initialFrezon * 0.5f, initialHalon * 0.5f));

        if (producedAmount <= 0 || initialHalon - producedAmount * 0.5f < 0 || initialFrezon - producedAmount * 0.5f < 0 || initialHalon > initialFrezon + initialHalon)
            return ReactionResult.NoReaction;

        mixture.AdjustMoles(Gas.Halon, -producedAmount * 0.5f);
        mixture.AdjustMoles(Gas.Frezon, -producedAmount * 0.5f);
        mixture.AdjustMoles(Gas.Helium, producedAmount * 1f);
        mixture.AdjustMoles(Gas.CarbonDioxide, producedAmount * 0.25f);
        mixture.AdjustMoles(Gas.Hydrogen, producedAmount * 2f);

        var energyReleased = producedAmount * Atmospherics.HalonFrezonDecompositionEnergy;

        var oldHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);
        var newHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);
        if (newHeatCapacity > Atmospherics.MinimumHeatCapacity)
            mixture.Temperature = Math.Max((mixture.Temperature * oldHeatCapacity + energyReleased) / newHeatCapacity, Atmospherics.TCMB);

        return ReactionResult.Reacting;
    }
}
