namespace Content.Server.Backmen.Procedural;

/// <summary>
/// Marks an entity that should remain inactive until its parent setup zone activates.
/// </summary>
[RegisterComponent]
public sealed partial class RoomSetupGatedComponent : Component
{
    [ViewVariables]
    public bool Activated;
}
