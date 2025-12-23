using Content.Shared._Backmen.Medical.SmartFridge;
using SmartFridgeComponent = Content.Shared._Backmen.Medical.SmartFridge.SmartFridgeComponent;

namespace Content.Client._Backmen.Medical.SmartFridge;

public sealed class SmartFridgeSystem : SharedSmartFridgeSystem
{
    [Dependency] private readonly SharedUserInterfaceSystem _uiSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SmartFridgeComponent, AfterAutoHandleStateEvent>(OnVendingAfterState);
    }

    private void OnVendingAfterState(EntityUid uid, SmartFridgeComponent component, ref AfterAutoHandleStateEvent args)
    {
        if (_uiSystem.TryGetOpenUi<SmartFridgeBoundUserInterface>(uid, SmartFridgeUiKey.Key, out var bui))
        {
            bui.Refresh();
        }
    }
}
