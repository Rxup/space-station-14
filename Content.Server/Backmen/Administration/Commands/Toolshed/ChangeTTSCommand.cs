using System.Linq;
using Content.Server.Administration;
using Content.Server.Backmen.Disease;
using Content.Server.Corvax.TTS;
using Content.Server.Humanoid;
using Content.Shared.Administration;
using Content.Shared.Backmen.Disease;
using Content.Shared.Corvax.TTS;
using Content.Shared.Humanoid;
using Robust.Shared.Prototypes;
using Robust.Shared.Toolshed;
using Robust.Shared.Toolshed.TypeParsers;

namespace Content.Server.Backmen.Administration.Commands.Toolshed;

[ToolshedCommand, AdminCommand(AdminFlags.Fun)]
public sealed class ChangeTTSCommand : ToolshedCommand
{
    [CommandImplementation]
    public EntityUid? ChangeTTS(
        [PipedArgument] EntityUid input,
        [CommandArgument] ProtoId<TTSVoicePrototype> prototype
    )
    {
        if (EntityManager.TryGetComponent<HumanoidProfileComponent>(input, out _))
        {
            var ttsComp = EnsureComp<TTSComponent>(input);
            ttsComp.VoicePrototypeId = prototype.Id;
            EntityManager.Dirty(input, ttsComp);
            return input;
        }

        var fallbackTts = EnsureComp<TTSComponent>(input);
        fallbackTts.VoicePrototypeId = prototype.Id;
        EntityManager.Dirty(input, fallbackTts);

        return input;
    }

    [CommandImplementation]
    public IEnumerable<EntityUid> ChangeTTS(
        [PipedArgument] IEnumerable<EntityUid> input,
        [CommandArgument] ProtoId<TTSVoicePrototype> prototype
    )
        => input.Select(x => ChangeTTS(x, prototype)).Where(x => x is not null).Select(x => (EntityUid) x!);
}
