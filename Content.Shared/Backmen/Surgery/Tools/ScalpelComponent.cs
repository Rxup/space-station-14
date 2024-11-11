using Robust.Shared.GameStates;

namespace Content.Shared.Backmen.Surgery.Tools;

[RegisterComponent, NetworkedComponent]
public sealed partial class ScalpelComponent : Component, ISurgeryToolComponent
{
    public string ToolName => "a scalpel";
    public bool? Used { get; set; } = null;
}
