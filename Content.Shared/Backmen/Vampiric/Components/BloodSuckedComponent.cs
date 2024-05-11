using Robust.Shared.GameStates;

namespace Content.Shared.Backmen.Vampiric.Components;

/// <summary>
/// For entities who have been succed.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class BloodSuckedComponent : Component
{
    [ViewVariables]
    public EntityUid? BloodSuckerMindId;
}
