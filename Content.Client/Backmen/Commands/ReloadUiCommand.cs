using Robust.Shared.Console;
using Robust.Client.State;
//using Content.Client.Lobby;
using Content.Client.Gameplay;
using Robust.Client;
//using System.Threading.Tasks;
using Timer = Robust.Shared.Timing.Timer;
using Content.Client.MainMenu;

namespace Content.Client.Backmen.Commands;

public sealed partial class ReloadUiCommand : IConsoleCommand
{
    //[Dependency] private IUserInterfaceManager _userInterfaceManager = default!;
    [Dependency] private IStateManager _stateManager = default!;
    [Dependency] private IBaseClient _baseClient = default!;

    public string Command => "reloadui";
    public string Description => Loc.GetString("command-reloadui-description");
    public string Help => Loc.GetString("command-reloadui-help");

    public void Execute(IConsoleShell shell, string argStr, string[] args){
        if(_baseClient.RunLevel != ClientRunLevel.InGame){
            shell.WriteError("Not in game!");
        }
        if(_stateManager.CurrentState is GameplayState){
            _stateManager.RequestStateChange<MainScreen>();
            Timer.Spawn(TimeSpan.FromSeconds(1), () =>
            {
                _stateManager.RequestStateChange<GameplayState>();
            });
        }
    }
}
