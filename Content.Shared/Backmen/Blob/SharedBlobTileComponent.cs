using Robust.Shared.GameStates;

namespace Content.Shared.Backmen.Blob;

[NetworkedComponent]
public abstract partial class SharedBlobTileComponent : Component
{
    [DataField("color")]
    public Color Color = Color.White;
}
