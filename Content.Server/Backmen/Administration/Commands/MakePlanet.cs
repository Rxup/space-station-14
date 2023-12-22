using Content.Server.Administration;
using Content.Server.Administration.Logs;
using Content.Server.Backmen.MakePlanet;
using Content.Shared.Administration;
using Content.Shared.Database;
using Robust.Shared.Console;

namespace Content.Server.Backmen.Administration.Commands;

[AdminCommand(AdminFlags.Mapping)]
public sealed class MakePlanet : IConsoleCommand
{
    [Dependency] private readonly IAdminLogManager _adminLogger = default!;
    [Dependency] private readonly EntityManager EntityManager = default!;

    public string Command => "makeplanet";

    public string Description => "Для ивента Колонизация. Создаёт новую карту с биомом Continental, спавнит на нём доступную FTL точку, делает объявление в консоль связи и даёт новую цель станции.";

    public string Help => "makeplanet";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
		var planetsys = EntityManager.System<MakePlanetSystem>();
		planetsys.ExecuteColonizeEvent();
		_adminLogger.Add(LogType.AdminMessage, LogImpact.Extreme, $"Admin {(shell.Player != null ? shell.Player.Name : "An administrator")} started Colonization event");
    }
}
