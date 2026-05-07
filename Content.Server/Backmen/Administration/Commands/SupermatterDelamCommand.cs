using Content.Server.Administration;
using Content.Shared.Administration;
using Content.Shared.Backmen.Supermatter;
using Content.Shared.Backmen.Supermatter.Components;
using Robust.Shared.Console;

namespace Content.Server.Backmen.Administration.Commands;

[AdminCommand(AdminFlags.Fun)]
public sealed class SupermatterDelamCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entMan = default!;

    public string Command => "smdelam";
    public string Description => "Forces a supermatter crystal into delamination mode.";
    public string Help => "smdelam <supermatterUid> [Explosion|Singulo|Tesla]";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length is < 1 or > 3)
        {
            shell.WriteLine(Loc.GetString("shell-wrong-arguments-number"));
            return;
        }

        if (!NetEntity.TryParse(args[0], out var uidNet) || !_entMan.TryGetEntity(uidNet, out var uid))
        {
            shell.WriteLine(Loc.GetString("shell-invalid-entity-id"));
            return;
        }

        if (!_entMan.TryGetComponent<BkmSupermatterComponent>(uid, out var supermatter))
        {
            shell.WriteLine($"Entity {args[0]} is not a supermatter crystal.");
            return;
        }

        if (args.Length >= 2)
        {
            if (!Enum.TryParse<DelamType>(args[1], true, out var forcedType) ||
                forcedType is DelamType.Cascade)
            {
                shell.WriteLine("Invalid delam type. Allowed: Explosion, Singulo, Tesla.");
                return;
            }

            supermatter.PreferredDelamType = forcedType;
            supermatter.Delamming = true;
        }
        else
        {
            // Let the normal supermatter logic choose the delam type next tick.
            supermatter.Delamming = false;
        }

        if (args.Length == 3)
        {
            supermatter.DelamTimer = TimeSpan.FromSeconds(double.Parse(args[2]));
        }

        supermatter.Damage = supermatter.DamageDelaminationPoint + 100;
        supermatter.DelamAnnounced = false;
        supermatter.DelamTimerAccumulator = TimeSpan.Zero;

        shell.WriteLine(
            args.Length == 2
                ? $"Supermatter {args[0]} forced into delam mode ({supermatter.PreferredDelamType})."
                : $"Supermatter {args[0]} forced into delam mode (auto type).");
    }

    public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length switch
        {
            1 => CompletionResult.FromHintOptions(CompletionHelper.Components<BkmSupermatterComponent>(args[0], _entMan), "Supermatter UID"),
            2 => CompletionResult.FromHintOptions(new[] { "Explosion", "Singulo", "Tesla" }, "Delam type"),
            3 => CompletionResult.FromHintOptions(new[] { "0", "30", "60" }, "Sec to delam"),
            _ => CompletionResult.Empty
        };
    }
}
