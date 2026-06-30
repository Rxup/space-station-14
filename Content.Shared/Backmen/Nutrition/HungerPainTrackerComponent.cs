using Robust.Shared.GameStates;

namespace Content.Shared.Backmen.Nutrition;

[RegisterComponent, NetworkedComponent]
public sealed partial class HungerPainTrackerComponent : Component
{
    [DataField]
    public float CurrentStarvingPain;

    [DataField]
    public bool StarvingOrganTraumaApplied;
}
