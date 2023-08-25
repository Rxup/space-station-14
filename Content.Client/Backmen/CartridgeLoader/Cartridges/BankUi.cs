using Content.Client.UserInterface.Fragments;
using Content.Shared.Backmen.CartridgeLoader.Cartridges;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;

namespace Content.Client.Backmen.CartridgeLoader.Cartridges;

public sealed partial class BankUi : UIFragment
{
    private BankUiFragment? _fragment;

    public override Control GetUIFragmentRoot()
    {
        return _fragment!;
    }

    public override void Setup(BoundUserInterface userInterface, EntityUid? fragmentOwner)
    {
        _fragment = new BankUiFragment();
    }

    public override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is not BankUiState bankState)
            return;

        _fragment?.UpdateState(bankState);
    }
}
