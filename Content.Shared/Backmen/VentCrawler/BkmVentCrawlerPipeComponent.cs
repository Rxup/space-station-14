using Robust.Shared.GameStates;

namespace Content.Shared.Backmen.VentCrawler;

/// <summary>
/// Marks a gas pipe as part of the vent crawler pipe network.
/// Used to eject crawlers when the pipe is unanchored or destroyed.
/// </summary>
[RegisterComponent, NetworkedComponent]
[Access(typeof(SharedVentCrawlerSystem))]
public sealed partial class BkmVentCrawlerPipeComponent : Component;
