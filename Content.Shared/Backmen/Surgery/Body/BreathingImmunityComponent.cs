namespace Content.Shared.Backmen.Surgery.Body;

/// <summary>
/// GoobStation: Disables a mobs need for air when this component is added.
/// It will neither breathe nor take airloss damage.
/// </summary>
[RegisterComponent]
public sealed partial class BreathingImmunityComponent : Component;
