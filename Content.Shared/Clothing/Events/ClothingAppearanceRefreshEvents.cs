// start-backmen: arachne
namespace Content.Shared.Clothing.Events;

/// <summary>
/// Raised on an entity during <see cref="Content.Client.Clothing.ClientClothingSystem"/> appearance refresh,
/// before the clothing stencil mask assert.
/// </summary>
[ByRefEvent]
public readonly record struct BeforeClothingAppearanceRefreshEvent;

/// <summary>
/// Raised on an entity after clothing slot visuals are updated and the stencil mask is reset to hidden.
/// </summary>
[ByRefEvent]
public readonly record struct AfterClothingAppearanceRefreshEvent;
// end-backmen: arachne
