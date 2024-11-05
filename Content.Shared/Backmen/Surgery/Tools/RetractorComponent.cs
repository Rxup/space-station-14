using Robust.Shared.GameStates;

namespace Content.Shared.Backmen.Surgery.Tools;

[RegisterComponent, NetworkedComponent]
public sealed partial class RetractorComponent : Component, ISurgeryToolComponent
{
    public string ToolName => "a retractor";
    public bool? Used { get; set; } = null;
}
