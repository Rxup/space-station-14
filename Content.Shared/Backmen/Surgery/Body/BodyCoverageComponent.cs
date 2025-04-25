using Content.Shared.Body.Part;

namespace Content.Shared.Backmen.Surgery.Body;

/// <summary>
/// Used for armor or armored entities to tell what body parts are covered with it's protection.
/// Mostly used by ArmorComponent and TemperatureProtectionComponent.
/// </summary>
[RegisterComponent]
public sealed partial class BodyCoverageComponent : Component
{
    // thankfully all the armor in the game is symmetrical.
    [DataField]
    public List<BodyPartType> Coverage = new();

    /// <summary>
    /// If true, the coverage won't show.
    /// </summary>
    [DataField("coverageHidden")]
    public bool ArmourCoverageHidden = false;
}
