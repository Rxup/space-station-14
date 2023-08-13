using Content.Server.Administration;
using Content.Server.Administration.Logs;
using Content.Server.Forensics;
using Content.Shared.Administration;
using Content.Shared.Inventory;
using Robust.Shared.Console;

namespace Content.Server.Backmen.Administration.Commands;

[AdminCommand(AdminFlags.Fun)]
public sealed class FakeFingerprint : IConsoleCommand
{
    [Dependency] private readonly IAdminLogManager _adminLogger = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;

    public string Command => "fakefingerprints";

    public string Description => "помещает отпечатки и ДНК жертвы на указанном объекте (волокна перчаток не берутся)";

    public string Help => "fakefingerprints <playerUid> <targetEntityUid>";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if(args.Length != 2){
            shell.WriteLine(Loc.GetString("shell-wrong-arguments-number"));
            return;
        }
        if(!EntityUid.TryParse(args[0], out var playerUid)){
            shell.WriteLine(Loc.GetString("shell-invalid-entity-id"));
            return;
        }
        if(!EntityUid.TryParse(args[1], out var item)){
            shell.WriteLine(Loc.GetString("shell-invalid-entity-id"));
            return;
        }
        var f = _entityManager.EnsureComponent<ForensicsComponent>(item);
        if (_entityManager.TryGetComponent<DnaComponent>(playerUid, out var dna))
        {
            f.DNAs.Add(dna.DNA);
        }

        var inventory = _entityManager.System<InventorySystem>();
        if (inventory.TryGetSlotEntity(playerUid, "gloves", out var gloves))
        {
            if (_entityManager.TryGetComponent<FiberComponent>(gloves, out var fiber) && !string.IsNullOrEmpty(fiber.FiberMaterial))
                f.Fibers.Add(string.IsNullOrEmpty(fiber.FiberColor) ? Loc.GetString("forensic-fibers", ("material", fiber.FiberMaterial)) : Loc.GetString("forensic-fibers-colored", ("color", fiber.FiberColor), ("material", fiber.FiberMaterial)));
        }
        if (_entityManager.TryGetComponent<FingerprintComponent>(playerUid, out var fingerprint))
        {
            f.Fingerprints.Add(fingerprint.Fingerprint ?? "");
        }
    }
    public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length switch
        {
            1 => CompletionResult.FromHintOptions(CompletionHelper.EntityUids(args[0], _entityManager),
                "Персонаж"),
            2 => CompletionResult.FromHintOptions(CompletionHelper.EntityUids(args[1], _entityManager),
                "Целевой предмет"),
            _ => CompletionResult.Empty
        };
    }
}
