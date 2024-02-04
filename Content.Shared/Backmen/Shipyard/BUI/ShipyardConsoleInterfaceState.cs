using Robust.Shared.Serialization;

namespace Content.Shared.Backmen.Shipyard.BUI;

[NetSerializable, Serializable]
public sealed class ShipyardConsoleInterfaceState : BoundUserInterfaceState
{
    public int Balance;
    public readonly bool AccessGranted;
    public List<string> AllowedGroup;

    public ShipyardConsoleInterfaceState(
        int balance,
        bool accessGranted,
        List<string> allowedGroup)
    {
        Balance = balance;
        AccessGranted = accessGranted;
        AllowedGroup = allowedGroup;
    }
}
