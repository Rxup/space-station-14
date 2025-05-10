using Content.Shared.Backmen.TerrorSpider;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.UserInterface;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;

namespace Content.Client.Backmen.TerrorSpider;

[UsedImplicitly]
public sealed class EggsLayingBui : BoundUserInterface
{
    [Dependency] private readonly IInputManager _inputManager = default!;
    [Dependency] private readonly IClyde _displayManager = default!;

    private EggsLayingMenu? _menu;

    public EggsLayingBui(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        IoCManager.InjectDependencies(this);
    }

    protected override void Open()
    {
        _menu = this.CreateWindow<EggsLayingMenu>();

        _menu.OnClose += Close;
        _menu.EggChosen += OnEggChosen;

        var viewportSize = _displayManager.ScreenSize;
        _menu.OpenCenteredAt(_inputManager.MouseScreenPosition.Position / viewportSize);
    }

    private void OnEggChosen(EntProtoId egg)
    {
        SendMessage(new EggsLayingBuiMsg { Egg = egg });
        _menu?.Close();
        Close();
    }

    protected override void UpdateState(BoundUserInterfaceState? state)
    {

    }
}
