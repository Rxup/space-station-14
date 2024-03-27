using Content.Client.Backmen.Ghost.Roles.UI;
using Content.Client.UserInterface.Screens;
using Content.Client.UserInterface.Systems.Gameplay;
using Content.Shared.Backmen.Ghost.Roles;
using Content.Shared.Backmen.Ghost.Roles.Components;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.Backmen.Ghost.Roles;

public sealed class GhostRoleRollerSystem : SharedGhostRoleRollerSystem
{
    [Dependency] protected readonly IUserInterfaceManager UIManager = default!;
    private Control? _popupContainer;
    private readonly Dictionary<uint, UI.RollerPopup> _popups = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GhostVisRollerComponent, AfterAutoHandleStateEvent>(UpdateRoller);
        SubscribeLocalEvent<GhostVisRollerComponent, ComponentShutdown>(RemoveRoller);

        var gameplayStateLoad = UIManager.GetUIController<GameplayStateLoadController>();
        gameplayStateLoad.OnScreenLoad += OnScreenLoad;
        gameplayStateLoad.OnScreenUnload += OnScreenUnload;
    }

    public void ClearPopupContainer()
    {
        if (_popupContainer == null)
            return;

        if (!_popupContainer.Disposed)
        {
            foreach (var popup in _popups.Values)
            {
                popup.Orphan();
            }
        }

        _popups.Clear();
        _popupContainer = null;
    }

    public override void Shutdown()
    {
        base.Shutdown();

        var gameplayStateLoad = UIManager.GetUIController<GameplayStateLoadController>();
        gameplayStateLoad.OnScreenLoad -= OnScreenLoad;
        gameplayStateLoad.OnScreenUnload -= OnScreenUnload;
        ClearPopupContainer();
    }

    private void OnScreenUnload()
    {
        ClearPopupContainer();
    }

    private void OnScreenLoad()
    {
        switch (UIManager.ActiveScreen)
        {
            case DefaultGameScreen game:
                SetPopupContainer(game.RollerMenu);
                break;
            case SeparatedChatGameScreen separated:
                SetPopupContainer(separated.RollerMenu);
                break;
        }
    }

    private void SetPopupContainer(BoxContainer gameRollerMenu)
    {
        if (_popupContainer != null)
        {
            ClearPopupContainer();
        }

        _popupContainer = gameRollerMenu;
    }

    private void RemoveRoller(Entity<GhostVisRollerComponent> ent, ref ComponentShutdown args)
    {
        if(!_popups.ContainsKey(ent.Comp.CurrentId))
            return;
        _popups[ent.Comp.CurrentId].Orphan();
    }

    private uint _lastId = 0;
    private void UpdateRoller(Entity<GhostVisRollerComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (ent.Comp.CurrentId == 0)
        {
            if (_lastId != 0 && _popups.ContainsKey(_lastId))
            {
                _popups[_lastId].Orphan();
                _popups.Remove(_lastId);
            }
            return;
        }

        _lastId = ent.Comp.CurrentId;
        if (!_popups.ContainsKey(ent.Comp.CurrentId))
        {
            _popups.Add(ent.Comp.CurrentId, new RollerPopup(ent));
            _popupContainer?.AddChild(_popups[ent.Comp.CurrentId]);
        }

        _popups[ent.Comp.CurrentId].UpdateData();
    }
}
