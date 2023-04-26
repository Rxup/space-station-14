using Content.Server.Administration;
using Content.Server.Administration.Logs;
using Content.Server.Backmen.SpecForces;
using Content.Shared.Administration;
using Robust.Shared.Console;
using Content.Shared.Database;

namespace Content.Server.Backmen.Administration.Commands;

[AdminCommand(AdminFlags.Admin)]
public sealed class CallSpecForcesCommand : IConsoleCommand
{
    [Dependency] private readonly IAdminLogManager _adminLogger = default!;
    [Dependency] private readonly EntityManager EntityManager = default!;
    public string Command => "callspecforces";

    public string Description => "вызов обр/рзбзз/дед сквад";

    public string Help => "callspecforces";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if(args.Length != 1){
            shell.WriteLine(Loc.GetString("shell-wrong-arguments-number"));
            return;
        }
        if(!Enum.TryParse<SpecForcesType>(args[0], true, out var SpecType)){
            shell.WriteLine(Loc.GetString("shell-invalid-entity-id"));
            return;
        }
        if(!EntityManager.System<SpecForcesSystem>().CallOps(SpecType)){
            shell.WriteLine("В этом раунде уже был вызван данный тип");
        }

        _adminLogger.Add(LogType.AdminMessage, LogImpact.Extreme, $"Admin {(shell.Player != null ? shell.Player.Name : "An administrator")} SpecForcesSystem call {SpecType}");
    }

    public CompletionResult GetCompletion(IConsoleShell shell, string[] args){
        return args.Length switch
        {
            1 => CompletionResult.FromHintOptions(Enum.GetNames<SpecForcesType>(),
                "Тип вызова"),
            _ => CompletionResult.Empty
        };
    }
}
