using System.Linq;
using Content.Server.Administration;
using Content.Server.GameTicking;
using Content.Shared.Administration;
using Content.Shared.Backmen.Lobby;
using Robust.Shared.Console;
using Robust.Shared.Prototypes;

namespace Content.Server.Backmen.Administration.Commands;

[AdminCommand(AdminFlags.Fun)]
public sealed partial class LobbyBgNextCommand : LocalizedEntityCommands
{
    [Dependency] private GameTicker _ticker = default!;

    public override string Command => "lobby_bg_next";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (!_ticker.TryNextLobbyBackground(out var background) || background == null)
        {
            shell.WriteError(Loc.GetString("cmd-lobby_bg_next-none"));
            return;
        }

        shell.WriteLine(Loc.GetString("cmd-lobby_bg_next-success", ("background", background.Value.Id)));
    }
}

[AdminCommand(AdminFlags.Fun)]
public sealed partial class LobbyBgCommand : LocalizedEntityCommands
{
    [Dependency] private GameTicker _ticker = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;

    public override string Command => "lobby_bg";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length == 0)
        {
            var current = _ticker.LobbyBackground?.Id ?? Loc.GetString("cmd-lobby_bg-none-current");
            shell.WriteLine(Loc.GetString("cmd-lobby_bg-current", ("background", current)));
            return;
        }

        if (args.Length != 1)
        {
            shell.WriteLine(Loc.GetString("cmd-lobby_bg-help"));
            return;
        }

        var id = args[0];
        if (!_prototypes.TryIndex<AnimatedLobbyScreenPrototype>(id, out _))
        {
            shell.WriteError(Loc.GetString("cmd-lobby_bg-not-found", ("background", id)));
            return;
        }

        if (!_ticker.TrySetLobbyBackground(id))
        {
            shell.WriteError(Loc.GetString("cmd-lobby_bg-not-found", ("background", id)));
            return;
        }

        shell.WriteLine(Loc.GetString("cmd-lobby_bg-success", ("background", id)));
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length != 1)
            return CompletionResult.Empty;

        var options = _prototypes
            .EnumeratePrototypes<AnimatedLobbyScreenPrototype>()
            .OrderBy(p => p.ID)
            .Select(p => p.ID);

        return CompletionResult.FromHintOptions(options, Loc.GetString("cmd-lobby_bg-hint"));
    }
}
