using Content.Server.Actions;
using Content.Server.Administration;
using Content.Shared.Administration;
using Content.Shared.Backmen.Abilities.Psionics;
using Content.Shared.Mobs.Components;
using Robust.Shared.Console;
using Robust.Shared.Player;

namespace Content.Server.Backmen.Psionics;

[AdminCommand(AdminFlags.Logs)]
public sealed class ListPsionicsCommand : IConsoleCommand
{
    public string Command => "lspsionics";
    public string Description => Loc.GetString("command-lspsionic-description");
    public string Help => Loc.GetString("command-lspsionic-help");
    public async void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var entMan = IoCManager.Resolve<IEntityManager>();
        var action = entMan.System<ActionsSystem>();
        foreach (var (actor, mob, psionic, meta) in entMan.EntityQuery<ActorComponent, MobStateComponent, PsionicComponent, MetaDataComponent>())
        {
            entMan.TryGetComponent<MetaDataComponent>(psionic.PsionicAbility, out var skill);
            // filter out xenos, etc, with innate telepathy
            if (skill != null)
                shell.WriteLine(meta.EntityName + " (" + meta.Owner + ") - " + actor.PlayerSession.Name + " - " + Loc.GetString(skill.EntityName));
        }
    }
}
