using Content.Server.Administration;
using Content.Server.Database;
using Content.Shared.Administration;
using Content.Shared.CCVar;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Console;
using Robust.Shared.Network;

using Content.Server.Players; // backmen: whitelist

namespace Content.Server.Whitelist;

[AdminCommand(AdminFlags.Permissions)]
public sealed class AddWhitelistCommand : IConsoleCommand
{
    public string Command => "whitelistadd";
    public string Description => Loc.GetString("command-whitelistadd-description");
    public string Help => Loc.GetString("command-whitelistadd-help");
    public async void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1)
            return;

        var db = IoCManager.Resolve<IServerDbManager>();
        var loc = IoCManager.Resolve<IPlayerLocator>();

        var name = args[0];
        var data = await loc.LookupIdByNameAsync(name);
        var wlSystem = IoCManager.Resolve<EntityManager>().System<Backmen.RoleWhitelist.WhitelistSystem>(); // backmen: whitelist

        if (data != null)
        {
            var guid = data.UserId;
            var isWhitelisted = await db.GetWhitelistStatusAsync(guid);
            if (isWhitelisted)
            {
                shell.WriteLine(Loc.GetString("command-whitelistadd-existing", ("username", data.Username)));
                return;
            }

            await db.AddToWhitelistAsync(guid);

            wlSystem.AddWhitelist(guid); // backmen: whitelist

            shell.WriteLine(Loc.GetString("command-whitelistadd-added", ("username", data.Username)));
            return;
        }

        shell.WriteError(Loc.GetString("command-whitelistadd-not-found", ("username", args[0])));
    }
}

[AdminCommand(AdminFlags.Permissions)]
public sealed class RemoveWhitelistCommand : IConsoleCommand
{
    public string Command => "whitelistremove";
    public string Description => Loc.GetString("command-whitelistremove-description");
    public string Help => Loc.GetString("command-whitelistremove-help");
    public async void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1)
            return;

        var db = IoCManager.Resolve<IServerDbManager>();
        var loc = IoCManager.Resolve<IPlayerLocator>();


        var name = args[0];
        var data = await loc.LookupIdByNameAsync(name);
        var wlSystem = IoCManager.Resolve<EntityManager>().System<Backmen.RoleWhitelist.WhitelistSystem>(); // backmen: whitelist

        if (data != null)
        {
            var guid = data.UserId;
            var isWhitelisted = await db.GetWhitelistStatusAsync(guid);
            if (!isWhitelisted)
            {
                shell.WriteLine(Loc.GetString("command-whitelistremove-existing", ("username", data.Username)));
                return;
            }

            await db.RemoveFromWhitelistAsync(guid);

            wlSystem.RemoveWhitelist(guid); // backmen: whitelist

            shell.WriteLine(Loc.GetString("command-whitelistremove-removed", ("username", data.Username)));
            return;
        }

        shell.WriteError(Loc.GetString("command-whitelistremove-not-found", ("username", args[0])));
    }
}

[AdminCommand(AdminFlags.Permissions)]
public sealed class KickNonWhitelistedCommand : IConsoleCommand
{
    public string Command => "kicknonwhitelisted";
    public string Description => Loc.GetString("command-kicknonwhitelisted-description");
    public string Help => Loc.GetString("command-kicknonwhitelisted-help");
    public async void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 0)
            return;

        var cfg = IoCManager.Resolve<IConfigurationManager>();

        if (!cfg.GetCVar(CCVars.WhitelistEnabled))
            return;

        var player = IoCManager.Resolve<IPlayerManager>();
        var db = IoCManager.Resolve<IServerDbManager>();
        var net = IoCManager.Resolve<IServerNetManager>();

        var wlSystem = IoCManager.Resolve<EntityManager>().System<Backmen.RoleWhitelist.WhitelistSystem>(); // backmen: whitelist

        foreach (var session in player.NetworkedSessions)
        {
            if (await db.GetAdminDataForAsync(session.UserId) is not null)
                continue;

            if (!wlSystem.IsInWhitelist(session.UserId))
            {
                net.DisconnectChannel(session.ConnectedClient, Loc.GetString("whitelist-not-whitelisted"));
            }
        }

    }
}
