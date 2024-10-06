using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.Backmen.UI.Buttons;

public sealed class WhiteUICommandButton : WhiteCommandButton
{
    public Type? WindowType { get; set; }
    private DefaultWindow? _window;

    protected override void Execute(BaseButton.ButtonEventArgs obj)
    {
        if (WindowType == null)
            return;

        var windowInstance = IoCManager.Resolve<IDynamicTypeFactory>().CreateInstance(WindowType);
        if (windowInstance is not DefaultWindow window)
            return;

        _window = window;
        _window.OpenCentered();
    }
}
