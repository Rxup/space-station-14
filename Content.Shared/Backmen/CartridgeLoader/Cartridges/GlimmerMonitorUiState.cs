using Content.Shared.CartridgeLoader;
using Robust.Shared.Serialization;

namespace Content.Shared.Backmen.CartridgeLoader.Cartridges;

[Serializable, NetSerializable]
public sealed class GlimmerMonitorUiState : BoundUserInterfaceState
{
    public List<int> GlimmerValues;

    public GlimmerMonitorUiState(List<int> glimmerValues)
    {
        GlimmerValues = glimmerValues;
    }
}

[Serializable, NetSerializable]
public sealed class GlimmerMonitorSyncMessageEvent : CartridgeMessageEvent
{
    public GlimmerMonitorSyncMessageEvent()
    {}
}
