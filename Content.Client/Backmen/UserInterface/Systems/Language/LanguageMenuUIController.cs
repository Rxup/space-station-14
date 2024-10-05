using Content.Client.Backmen.Language;
using Content.Client.Backmen.Language.Systems;
using Content.Client.Gameplay;
using Content.Client.UserInterface.Controls;
using Content.Client.UserInterface.Systems.MenuBar.Widgets;
using Content.Shared.Input;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input.Binding;
using Robust.Shared.Utility;
using static Robust.Client.UserInterface.Controls.BaseButton;
using JetBrains.Annotations;
using Robust.Client.Input;

namespace Content.Client.Backmen.UserInterface.Systems.Language;

[UsedImplicitly]
public sealed class LanguageMenuUIController :
    UIController,
    IOnStateEntered<GameplayState>,
    IOnStateExited<GameplayState>,
    IOnSystemChanged<LanguageSystem>
{
    [Dependency] private readonly IInputManager _input = default!;
    public LanguageMenuWindow? LanguageWindow;

    private MenuButton? LanguageButton =>
        UIManager.GetActiveUIWidgetOrNull<GameTopMenuBar>()?.LanguageButton;

    public void OnStateEntered(GameplayState state)
    {
        DebugTools.Assert(LanguageWindow == null);

        LanguageWindow = UIManager.CreateWindow<LanguageMenuWindow>();
        LayoutContainer.SetAnchorPreset(LanguageWindow, LayoutContainer.LayoutPreset.CenterTop);

        LanguageWindow.OnClose += () =>
        {
            if (LanguageButton != null)
                LanguageButton.Pressed = false;
        };
        LanguageWindow.OnOpen += () =>
        {
            if (LanguageButton != null)
                LanguageButton.Pressed = true;
        };
    }


    public void OnStateExited(GameplayState state)
    {
        if (LanguageWindow == null)
            return;

        LanguageWindow.Dispose();
        LanguageWindow = null;
    }

    public void UnloadButton()
    {
        if (LanguageButton == null)
            return;

        LanguageButton.OnPressed -= LanguageButtonPressed;
    }

    public void LoadButton()
    {
        if (LanguageButton == null)
            return;

        LanguageButton.OnPressed += LanguageButtonPressed;
    }

    private void LanguageButtonPressed(ButtonEventArgs args)
    {
        ToggleWindow();
    }

    private void ToggleWindow()
    {
        if (LanguageWindow == null)
            return;

        LanguageButton?.SetClickPressed(!LanguageWindow.IsOpen);

        if (LanguageWindow.IsOpen)
            LanguageWindow.Close();
        else
            LanguageWindow.Open();
    }

    public void OnSystemLoaded(LanguageSystem system)
    {
        _input.SetInputCommand(ContentKeyFunctions.OpenLanguageMenu,
            InputCmdHandler.FromDelegate(_ => ToggleWindow()));
    }

    public void OnSystemUnloaded(LanguageSystem system)
    {
        LanguageWindow?.Dispose();
        CommandBinds.Unregister<LanguageMenuUIController>();
    }
}
