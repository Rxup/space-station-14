using Robust.Shared.GameStates;

namespace Content.Shared.Backmen.Overlays;

/// <summary>
///     Allows seeing disease/infection status icons above mobs (medical HUD).
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ShowDiseaseIconsComponent : Component;
