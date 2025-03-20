using System.Linq;
using Content.Server.Administration;
using Content.Server.Forensics;
using Content.Shared.Administration;
using Content.Shared.Forensics.Components;
using Content.Shared.Inventory;
using Robust.Shared.Player;
using Robust.Shared.Toolshed;
using Robust.Shared.Toolshed.Errors;
using Robust.Shared.Toolshed.Syntax;

namespace Content.Server.Backmen.Administration.Commands.Toolshed;

[ToolshedCommand, AdminCommand(AdminFlags.Fun)]
public sealed class MakeFakeFingerprintCommand : ToolshedCommand
{
    private InventorySystem? _inventory;

    [CommandImplementation]
    public EntityUid? MakeFakeFingerprint(
        [CommandInvocationContext] IInvocationContext ctx,
        [PipedArgument] EntityUid target,
        [CommandArgument] ValueRef<ICommonSession> playerRef
    )
    {
        var player = playerRef.Evaluate(ctx);
        if (player is null || player.AttachedEntity is null)
        {
            ctx.ReportError(new NotForServerConsoleError());
            return target;
        }

        var playerUid = player.AttachedEntity.Value;


        var f = EntityManager.EnsureComponent<ForensicsComponent>(target);
        if (EntityManager.TryGetComponent<DnaComponent>(playerUid, out var dna) && !string.IsNullOrEmpty(dna.DNA))
        {
            f.DNAs.Add(dna.DNA);
        }

        _inventory ??= GetSys<InventorySystem>();
        if (_inventory.TryGetSlotEntity(playerUid, "gloves", out var gloves))
        {
            if (EntityManager.TryGetComponent<FiberComponent>(gloves, out var fiber) &&
                !string.IsNullOrEmpty(fiber.FiberMaterial))
            {
                f.Fibers.Add(string.IsNullOrEmpty(fiber.FiberColor)
                    ? Loc.GetString("forensic-fibers", ("material", fiber.FiberMaterial))
                    : Loc.GetString("forensic-fibers-colored", ("color", fiber.FiberColor),
                        ("material", fiber.FiberMaterial)));
            }
        }

        if (EntityManager.TryGetComponent<FingerprintComponent>(playerUid, out var fingerprint))
        {
            f.Fingerprints.Add(fingerprint.Fingerprint ?? "");
        }

        return target;
    }

    [CommandImplementation]
    public IEnumerable<EntityUid> MakeFakeFingerprint(
        [CommandInvocationContext] IInvocationContext ctx,
        [PipedArgument] IEnumerable<EntityUid> target,
        [CommandArgument] ValueRef<ICommonSession> playerRef
    )
        => target.Select(x => MakeFakeFingerprint(ctx, x, playerRef)).Where(x => x is not null)
            .Select(x => (EntityUid) x!);
}
