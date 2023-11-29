using Content.Server.Administration;
using Content.Server.Backmen.Shipyard.Systems;
using Content.Shared.Administration;
using Content.Shared.Backmen.Shipyard.Prototypes;
using Robust.Shared.Console;
using Robust.Shared.Prototypes;

namespace Content.Server.Backmen.Shipyard.Commands;

/// <summary>
/// Purchases a shuttle and docks it to a station.
/// </summary>
[AdminCommand(AdminFlags.Fun)]
public sealed class PurchaseShuttleCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    public string Command => "purchaseshuttle";
    public string Description => "Spawns and docks a specified shuttle from a grid file";
    public string Help => $"{Command} <station ID> <gridfile path>";
    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 2)
        {
            shell.WriteError("Not enough arguments.");
            return;
        }

        if (!int.TryParse(args[0], out var stationId))
        {
            shell.WriteError($"{args[0]} is not a valid integer.");
            return;
        }

        if (!_prototypeManager.TryIndex<VesselPrototype>(args[1], out var vessel))
        {
            shell.WriteError($"{args[1]} is not a valid vessel.");
            return;
        }

        var system = _entityManager.System<ShipyardSystem>();
        var stationNet = new NetEntity(stationId);
        if(stationNet.Valid && _entityManager.TryGetEntity(stationNet, out var station))
            system.TryPurchaseShuttle(station.Value, vessel, out _);
    }

    public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        switch (args.Length)
        {
            case 1:
                return CompletionResult.FromHint(Loc.GetString("station-id"));
            case 2:
                var opts = CompletionHelper.PrototypeIDs<VesselPrototype>();
                return CompletionResult.FromHintOptions(opts, Loc.GetString("cmd-hint-savemap-path"));
        }

        return CompletionResult.Empty;
    }
}
