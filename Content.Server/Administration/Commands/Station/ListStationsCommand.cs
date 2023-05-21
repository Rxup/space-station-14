using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server.Administration.Commands.Station;

[AdminCommand(AdminFlags.Admin)]
public sealed class ListStationsCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entityManager = default!;

    public string Command => "lsstations";

    public string Description => "List all active stations";

    public string Help => "lsstations";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var query = _entityManager.EntityQueryEnumerator<StationDataComponent>();

        while (query.MoveNext(out var station, out _))
        {
            var name = _entityManager.GetComponent<MetaDataComponent>(station).EntityName;
            shell.WriteLine($"{station, -10} | {name}");
        }
    }
}
