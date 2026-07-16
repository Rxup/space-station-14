using Content.Shared.Atmos;
using Content.Shared.Backmen.Supermatter.Monitor;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Backmen.Supermatter.Consoles;

[RegisterComponent, NetworkedComponent]
[Access(typeof(SharedSupermatterConsoleSystem))]
public sealed partial class SupermatterConsoleComponent : Component
{
    /// <summary>
    /// The current entity of interest (selected via the console UI)
    /// </summary>
    [ViewVariables]
    public NetEntity? FocusSupermatter;
}

[Serializable, NetSerializable]
public struct SupermatterFocusData
{
    public NetEntity NetEntity;
    public float Integrity;
    public float Power;
    public float Radiation;
    public float AbsorbedMoles;
    public float Temperature;
    public float TemperatureLimit;
    public float WasteMultiplier;
    public float AbsorptionRatio;
    public Dictionary<Gas, float> GasStorage;

    public SupermatterFocusData(
        NetEntity netEntity,
        float integrity,
        float power,
        float radiation,
        float absorbedMoles,
        float temperature,
        float temperatureLimit,
        float wasteMultiplier,
        float absorptionRatio,
        Dictionary<Gas, float> gasStorage)
    {
        NetEntity = netEntity;
        Integrity = integrity;
        Power = power;
        Radiation = radiation;
        AbsorbedMoles = absorbedMoles;
        Temperature = temperature;
        TemperatureLimit = temperatureLimit;
        WasteMultiplier = wasteMultiplier;
        AbsorptionRatio = absorptionRatio;
        GasStorage = gasStorage;
    }
}

[Serializable, NetSerializable]
public sealed class SupermatterConsoleBoundInterfaceState : BoundUserInterfaceState
{
    public SupermatterConsoleEntry[] Supermatters;
    public SupermatterFocusData? FocusData;

    public SupermatterConsoleBoundInterfaceState(SupermatterConsoleEntry[] supermatters, SupermatterFocusData? focusData)
    {
        Supermatters = supermatters;
        FocusData = focusData;
    }
}

[Serializable, NetSerializable]
public struct SupermatterConsoleEntry
{
    public NetEntity NetEntity;
    public string EntityName;
    public SupermatterStatusType EntityStatus;

    public SupermatterConsoleEntry(NetEntity entity, string entityName, SupermatterStatusType status)
    {
        NetEntity = entity;
        EntityName = entityName;
        EntityStatus = status;
    }
}

[Serializable, NetSerializable]
public sealed class SupermatterConsoleFocusChangeMessage : BoundUserInterfaceMessage
{
    public NetEntity? FocusSupermatter;

    public SupermatterConsoleFocusChangeMessage(NetEntity? focusSupermatter)
    {
        FocusSupermatter = focusSupermatter;
    }
}

[NetSerializable, Serializable]
public enum SupermatterConsoleVisuals
{
    ComputerLayerScreen,
}

[Serializable, NetSerializable]
public enum SupermatterConsoleUiKey
{
    Key,
}
