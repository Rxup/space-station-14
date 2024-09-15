using Content.Shared.Backmen.Soul;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client.Backmen.Soul;

/// <summary>
/// Initializes a <see cref="GolemWindow"/> and updates it when new server messages are received.
/// </summary>
[UsedImplicitly]
public sealed class GolemBoundUserInterface : BoundUserInterface
{
    private GolemWindow? _window;

    public GolemBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<GolemWindow>();
        if (State != null)
            UpdateState(State);

        _window.OnNameEntered += OnNameChanged;
        _window.OnMasterEntered += OnMasterChanged;
        _window.OnInstallButtonPressed += _ =>
        {
            SendMessage(new GolemNameChangedMessage(_window.GetCurrentName()));
            SendMessage(new GolemMasterNameChangedMessage(_window.GetCurrentMaster()));
            SendMessage(new GolemInstallRequestMessage());
        };
    }

    private void OnNameChanged(string newName)
    {
        SendMessage(new GolemNameChangedMessage(newName));
    }

    private void OnMasterChanged(string newMaster)
    {
        SendMessage(new GolemMasterNameChangedMessage(newMaster));
    }

    /// <summary>
    /// Update the UI state based on server-sent info
    /// </summary>
    /// <param name="state"></param>
    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (_window == null || state is not GolemBoundUserInterfaceState cast)
            return;

        _window.SetCurrentName(cast.Name);
        _window.SetCurrentMaster(cast.MasterName);
    }
}
