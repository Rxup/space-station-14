using Robust.Shared.Configuration;

// ReSharper disable once CheckNamespace
namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>
    ///     Temperature conduction tickrate in TPS. Temperature conduction processing will happen every 1/TPS seconds.
    /// </summary>
    public static readonly CVarDef<float> TemperatureTickRate =
        CVarDef.Create("temperature.tickrate", 2f, CVar.SERVERONLY);

    /// <summary>
    ///     Temperature conduction speed modifier. Because this is a game, players except things to happen faster.
    /// </summary>
    public static readonly CVarDef<float> TemperatureSpeedup =
        CVarDef.Create("temperature.speed_modifier", 8f, CVar.SERVER | CVar.REPLICATED);  // One must imagine TemperatureSystem prediction happy.
}
