using Robust.Shared.Serialization;

namespace Content.Shared.Tools.Components;

public abstract partial class SharedWeldableComponent : Component
{

}

[Serializable, NetSerializable]
public enum WeldableVisuals : byte
{
    IsWelded
}

[Serializable, NetSerializable]
public enum WeldableLayers : byte
{
    BaseWelded
}
