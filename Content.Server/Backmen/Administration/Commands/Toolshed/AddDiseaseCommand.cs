using System.Linq;
using Content.Server.Administration;
using Content.Server.Backmen.Disease;
using Content.Server.Polymorph.Systems;
using Content.Shared.Administration;
using Content.Shared.Backmen.Disease;
using Content.Shared.Polymorph;
using Robust.Shared.Toolshed;
using Robust.Shared.Toolshed.TypeParsers;

namespace Content.Server.Backmen.Administration.Commands.Toolshed;

[ToolshedCommand, AdminCommand(AdminFlags.Fun)]
public sealed class AddDiseaseCommand : ToolshedCommand
{
    private DiseaseSystem? _diseaseSystem;

    [CommandImplementation]
    public EntityUid? AddDisease(
        [PipedArgument] EntityUid input,
        [CommandArgument] Prototype<DiseasePrototype> prototype
    )
    {
        _diseaseSystem ??= GetSys<DiseaseSystem>();

        _diseaseSystem.TryAddDisease(input, prototype.Id);
        return input;
    }

    [CommandImplementation]
    public IEnumerable<EntityUid> AddDisease(
        [PipedArgument] IEnumerable<EntityUid> input,
        [CommandArgument] Prototype<DiseasePrototype> prototype
    )
        => input.Select(x => AddDisease(x, prototype)).Where(x => x is not null).Select(x => (EntityUid) x!);
}
