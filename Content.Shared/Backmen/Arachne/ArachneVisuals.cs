using Robust.Shared.Serialization;

namespace Content.Shared.Backmen.Arachne;

[Serializable, NetSerializable]
public enum ArachneVisuals
{
    ClothingStencil,
}

/// <summary>
/// Drives <see cref="HumanoidVisualLayers.StencilMask"/> via <see cref="AppearanceComponent"/> (see Harpy singing layer).
/// </summary>
[Serializable, NetSerializable]
public enum ArachneClothingStencilState
{
    Hidden,
    MaleNone,
    MaleTop,
    MaleFull,
    FemaleNone,
    FemaleTop,
    FemaleFull,
    UnisexNone,
    UnisexTop,
    UnisexFull,
}
