using Content.Client.UserInterface.Fragments;
using Content.Shared._Backmen.CartridgeLoader.Cartridges;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;

namespace Content.Client._Backmen.CartridgeLoader.Cartridges;

public sealed partial class BankUi : UIFragment
{
    public BankUiFragment? Fragment;

    public override Control GetUIFragmentRoot()
    {
        return Fragment!;
    }

    public override void Setup(BoundUserInterface userInterface, EntityUid? fragmentOwner)
    {
        Fragment = new BankUiFragment();
        Fragment.UpdateEntity(fragmentOwner);
    }

    public override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is not BankUiState bankState)
            return;

        Fragment?.UpdateState(bankState);
    }
}
