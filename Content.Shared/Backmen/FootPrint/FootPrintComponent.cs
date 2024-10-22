using Robust.Shared.GameStates;

namespace Content.Shared.Backmen.FootPrint;

/// <summary>
/// This is used for marking footsteps, handling footprint drawing.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FootPrintComponent : Component
{
    /// <summary>
    /// Owner (with <see cref="FootPrintsComponent"/>) of a print (this component).
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField("printOwner"), AutoNetworkedField]
    public EntityUid PrintOwner;

    [ViewVariables(VVAccess.ReadWrite), DataField("solution", required: true)]
    public string SolutionName = "step";
}
