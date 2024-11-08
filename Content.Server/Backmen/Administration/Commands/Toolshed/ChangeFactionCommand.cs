using System.Diagnostics;
using System.Linq;
using Content.Server.Administration;
using Content.Shared.Administration;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Prototypes;
using Content.Shared.NPC.Systems;
using Robust.Shared.Toolshed;
using Robust.Shared.Toolshed.Errors;
using Robust.Shared.Toolshed.TypeParsers;
using Robust.Shared.Utility;

namespace Content.Server.Backmen.Administration.Commands.Toolshed;

[ToolshedCommand, AdminCommand(AdminFlags.Fun)]
public sealed class ChangeFactionCommand : ToolshedCommand
{
    private NpcFactionSystem? _npcFactionSystem;

    #region Add

    [CommandImplementation("addFaction")]
    private EntityUid AddFaction(
        [CommandInvocationContext] IInvocationContext ctx,
        [PipedArgument] EntityUid input,
        [CommandArgument] Prototype<NpcFactionPrototype> factionData
    )
    {
        _npcFactionSystem ??= GetSys<NpcFactionSystem>();
        _npcFactionSystem.AddFaction(input, factionData.Id);
        return input;
    }

    [CommandImplementation("addFaction")]
    private IEnumerable<EntityUid> AddFaction(
        [CommandInvocationContext] IInvocationContext ctx,
        [PipedArgument] IEnumerable<EntityUid> input,
        [CommandArgument] Prototype<NpcFactionPrototype> factionData
    )
    {
        return input.Select(member => AddFaction(ctx, member, factionData));
    }

    #endregion

    #region Remove

    [CommandImplementation("rmFaction")]
    private EntityUid RemoveFaction(
        [CommandInvocationContext] IInvocationContext ctx,
        [PipedArgument] EntityUid input,
        [CommandArgument] Prototype<NpcFactionPrototype> factionData
    )
    {
        _npcFactionSystem ??= GetSys<NpcFactionSystem>();
        _npcFactionSystem.RemoveFaction(input, factionData.Id);
        return input;
    }

    [CommandImplementation("rmFaction")]
    private IEnumerable<EntityUid> RemoveFaction(
        [CommandInvocationContext] IInvocationContext ctx,
        [PipedArgument] IEnumerable<EntityUid> input,
        [CommandArgument] Prototype<NpcFactionPrototype> factionData
    )
    {
        return input.Select(member => RemoveFaction(ctx, member, factionData));
    }

    #endregion

    #region Clear

    [CommandImplementation("clearFaction")]
    private EntityUid ClearFaction(
        [CommandInvocationContext] IInvocationContext ctx,
        [PipedArgument] EntityUid input
    )
    {
        _npcFactionSystem ??= GetSys<NpcFactionSystem>();
        _npcFactionSystem.ClearFactions(input);
        return input;
    }

    [CommandImplementation("clearFaction")]
    private IEnumerable<EntityUid> ClearFaction(
        [CommandInvocationContext] IInvocationContext ctx,
        [PipedArgument] IEnumerable<EntityUid> input
    )
    {
        return input.Select(member => ClearFaction(ctx, member));
    }

    #endregion
}

public record struct FactionCompNotExists : IConError
{
    public FormattedMessage DescribeInner()
    {
        return FormattedMessage.FromMarkupOrThrow("У сущности нет компонента NpcFactionMember.");
    }

    public string? Expression { get; set; }
    public Vector2i? IssueSpan { get; set; }
    public StackTrace? Trace { get; set; }
}
