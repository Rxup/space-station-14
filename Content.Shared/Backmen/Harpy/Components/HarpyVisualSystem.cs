using Robust.Shared.Serialization;

namespace Content.Shared.Backmen.Harpy.Components;

[Serializable, NetSerializable]
public enum HarpyVisualLayers
{
    Singing,
}

[Serializable, NetSerializable]
public enum SingingVisualLayer
{
    True,
    False,
}
