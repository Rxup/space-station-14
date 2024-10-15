using Robust.Shared.GameStates;

namespace Content.Shared.Backmen.FootPrints;

/// <summary>
/// This is used for marking footsteps, handling footprint drawing.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FootPrintComponent : Component
{
    /// <summary>
    /// Owner of a print.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField("printOwner"), AutoNetworkedField]
    public EntityUid PrintOwner;

    [ViewVariables(VVAccess.ReadWrite), DataField("solution", required: true)]
    public string SolutionName = "step";
}
