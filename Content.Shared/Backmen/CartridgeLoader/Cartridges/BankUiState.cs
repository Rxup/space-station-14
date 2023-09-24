using Content.Shared.FixedPoint;
using Robust.Shared.Serialization;

namespace Content.Shared.Backmen.CartridgeLoader.Cartridges;

[Serializable, NetSerializable]
public sealed class BankUiState : BoundUserInterfaceState
{
    public FixedPoint2? LinkedAccountBalance;
    public BankUiState(
        FixedPoint2 ? linkedAccountBalance = null
        )
    {
        LinkedAccountBalance = linkedAccountBalance;
    }
}
