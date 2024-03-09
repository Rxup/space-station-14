using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Backmen.Blob;

[NetworkedComponent]
public abstract partial class SharedBlobbernautComponent : Component
{
    [DataField("color")]
    public Color Color = Color.White;
}
