namespace Content.Shared._Lavaland.Shuttles;

/// <summary>
/// Directed after <c>ApcPowerReceiverComponent</c> map-init in <c>PowerNetSystem</c>.
/// Avoids a second <c>(ApcPowerReceiverComponent, MapInitEvent)</c> subscription.
/// </summary>
[ByRefEvent]
public readonly record struct ApcPowerReceiverMapInitEvent;
