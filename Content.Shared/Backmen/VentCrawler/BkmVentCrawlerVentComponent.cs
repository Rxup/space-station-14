using Robust.Shared.GameStates;

namespace Content.Shared.Backmen.VentCrawler;

/// <summary>
/// Marks a gas vent as an entry and exit point for vent crawlers.
/// </summary>
[RegisterComponent, NetworkedComponent]
[Access(typeof(SharedVentCrawlerSystem))]
public sealed partial class BkmVentCrawlerVentComponent : Component;
