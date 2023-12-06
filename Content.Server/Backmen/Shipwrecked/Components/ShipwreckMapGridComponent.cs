namespace Content.Server.Backmen.Shipwrecked.Components;

[RegisterComponent]
public sealed partial class ShipwreckMapGridComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite)]
    public Box2 Area { get; set; }

    /// <summary>
    /// Currently active chunks
    /// </summary>
    [DataField("loadedChunks")]
    public HashSet<Vector2i> LoadedChunks = new();
}
