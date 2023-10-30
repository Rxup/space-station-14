using Robust.Shared.Serialization;

namespace Content.Shared.Backmen.Blob;

[Serializable, NetSerializable]
public sealed class BlobbernautComponentState : ComponentState
{
    public Color Color;
}
