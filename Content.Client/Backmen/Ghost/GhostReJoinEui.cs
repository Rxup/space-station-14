using Content.Client.Backmen.Ghost.UI;
using Content.Client.Eui;
using Content.Shared.Backmen.Ghost;
using Content.Shared.Eui;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client.Backmen.Ghost;

[UsedImplicitly]
public sealed class GhostReJoinEui : BaseEui
{
    private GhostReJoinSelect _window;

    public GhostReJoinEui()
    {
        _window = new GhostReJoinSelect();
        _window.OnRejoinSelectSelected += WindowOnOnRejoinSelectSelected;
        _window.OnRejoinSelectRandom += WindowOnOnRejoinSelectRandom;
        _window.OnClose += () => SendMessage(new CloseEuiMessage());
    }

    private void WindowOnOnRejoinSelectRandom(object? sender, NetEntity e)
    {
        SendMessage(new GhostReJoinRandomMessage(e));
    }

    private void WindowOnOnRejoinSelectSelected(object? sender, (NetEntity station, int character) e)
    {
        SendMessage(new GhostReJoinCharacterMessage(e.character, e.station));
    }

    public override void HandleState(EuiStateBase state)
    {
        if (state is not GhostReJoinInterfaceState cast)
            return;

        _window.UpdateState(cast);
    }

    public override void Closed()
    {
        base.Closed();
        _window.Close();
    }

    public override void Opened()
    {
        _window.OpenCentered();
    }
}
