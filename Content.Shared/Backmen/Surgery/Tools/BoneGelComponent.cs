using Robust.Shared.GameStates;

namespace Content.Shared.Backmen.Surgery.Tools;

[RegisterComponent, NetworkedComponent]
public sealed partial class BoneGelComponent : Component, ISurgeryToolComponent
{
    public string ToolName => "bone gel";

    public bool? Used { get; set; } = null;
}
