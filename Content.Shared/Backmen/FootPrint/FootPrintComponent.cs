using Content.Shared.Chemistry.Components;
using Robust.Shared.GameStates;

namespace Content.Shared.Backmen.FootPrint;

/// <summary>
/// This is used for marking footsteps, handling footprint drawing.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class FootPrintComponent : Component
{
    [DataField("solution")] public string SolutionName = "step";
    public Entity<SolutionComponent>? Solution;
}
