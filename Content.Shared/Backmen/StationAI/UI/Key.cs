using Robust.Shared.Serialization;

namespace Content.Shared.Backmen.StationAI.UI;

[Serializable, NetSerializable]
public enum AICameraListUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public enum AiVisuals : byte
{
    InEye,
    Dead
}

[Serializable, NetSerializable]
public enum AiVisualLayers : byte
{
    Dead,
    NotInEye
}
