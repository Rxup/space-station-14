using Content.Server.Administration;
using Content.Server.Administration.Logs;
using Content.Server.Backmen.SpecForces;
using Content.Shared.Administration;
using Robust.Shared.Console;
using Content.Shared.Database;
using Robust.Shared.Prototypes;

namespace Content.Server.Backmen.Administration.Commands;

[AdminCommand(AdminFlags.Admin)]
public sealed class CallSpecForcesCommand : IConsoleCommand
{
    [Dependency] private readonly IAdminLogManager _adminLogger = default!;
    [Dependency] private readonly EntityManager EntityManager = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    public string Command => "callspecforces";

    public string Description => "Вызов команды спецсил";

    public string Help => "callspecforces";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if(args.Length != 1)
        {
            shell.WriteLine(Loc.GetString("shell-wrong-arguments-number"));
            return;
        }
        var specsys = EntityManager.System<SpecForcesSystem>();
        if(!specsys.CallOps(args[0],shell.Player != null ? shell.Player.Name : "An administrator")){
            shell.WriteLine($"Подождите еще {specsys.DelayTime} перед запуском следующих!");
        }

        _adminLogger.Add(LogType.AdminMessage, LogImpact.Extreme, $"Admin {(shell.Player != null ? shell.Player.Name : "An administrator")} SpecForcesSystem call {args[0]}");
    }
    public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length switch
        {
            1 => CompletionResult.FromHintOptions(
                CompletionHelper.PrototypeIDs<SpecForceTeamPrototype>
                    (true, _prototypes), "Тип вызова"),
            _ => CompletionResult.Empty
        };
    }
}
