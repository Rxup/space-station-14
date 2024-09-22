using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using JetBrains.Annotations;

namespace Content.Server.Atmos.Reactions
{
    [UsedImplicitly]
    [DataDefinition]
    public sealed partial class HydrogenFireReaction : IGasReactionEffect
    {
        public ReactionResult React(GasMixture mixture, IGasMixtureHolder? holder, AtmosphereSystem atmosphereSystem, float heatScale)
        {
            var initialHyperNoblium = mixture.GetMoles(Gas.HyperNoblium);
            if (initialHyperNoblium >= 5.0f && mixture.Temperature > 20f)
                return ReactionResult.NoReaction;

            var energyReleased = 0f;
            var oldHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);
            var temperature = mixture.Temperature;
            var location = holder as TileAtmosphere;
            mixture.ReactionResults[GasReaction.Fire] = 0;

            var initialOxygen = mixture.GetMoles(Gas.Oxygen);
            var initialHydrogen = mixture.GetMoles(Gas.Hydrogen);

            var burnedFuel = Math.Min(initialHydrogen / Atmospherics.FireH2BurnRateDelta, Math.Min(initialOxygen / (Atmospherics.FireH2BurnRateDelta * Atmospherics.H2OxygenFullBurn), Math.Min(initialHydrogen, initialOxygen * 0.5f)));

            if (burnedFuel > 0)
            {
                energyReleased += Atmospherics.FireH2EnergyReleased * burnedFuel;

                mixture.AdjustMoles(Gas.WaterVapor, burnedFuel);
                mixture.AdjustMoles(Gas.Hydrogen, -burnedFuel);
                mixture.AdjustMoles(Gas.Oxygen, -burnedFuel * 0.5f);

                mixture.ReactionResults[GasReaction.Fire] += burnedFuel;
            }

            if (energyReleased > 0)
            {
                var newHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);
                if (newHeatCapacity > Atmospherics.MinimumHeatCapacity)
                    mixture.Temperature = (temperature * oldHeatCapacity + energyReleased) / newHeatCapacity;
            }

            if (location != null)
            {
                temperature = mixture.Temperature;
                if (temperature > Atmospherics.FireMinimumTemperatureToExist)
                {
                    atmosphereSystem.HotspotExpose(location.GridIndex, location.GridIndices, temperature, mixture.Volume);
                }
            }

            return mixture.ReactionResults[GasReaction.Fire] != 0 ? ReactionResult.Reacting : ReactionResult.NoReaction;
        }
    }
}
