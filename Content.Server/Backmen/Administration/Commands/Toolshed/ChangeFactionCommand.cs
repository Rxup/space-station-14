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
    private EntityQuery<NpcFactionMemberComponent>? _npcFactionMemberQuery;

    #region Base

    #region Add

    [CommandImplementation("addFaction")]
    private EntityUid? AddFaction(
        [CommandInvocationContext] IInvocationContext ctx,
        [PipedArgument] EntityUid input,
        [CommandArgument] Prototype<NpcFactionPrototype> factionData,
        [CommandArgument] bool ensure = true
    )
    {
        if (ensure)
        {
            EnsureComp<NpcFactionMemberComponent>(input);
        }
        else
        {
            if (!HasComp<NpcFactionMemberComponent>(input))
            {
                ctx.ReportError(new ComponentNotExists());
                return null;
            }
        }

        _npcFactionSystem ??= GetSys<NpcFactionSystem>();
        _npcFactionSystem.AddFaction(input, factionData.Id);
        return input;
    }

    [CommandImplementation("addFaction")]
    private IEnumerable<EntityUid> AddFaction(
        [CommandInvocationContext] IInvocationContext ctx,
        [PipedArgument] IEnumerable<EntityUid> input,
        [CommandArgument] Prototype<NpcFactionPrototype> factionData,
        [CommandArgument] bool ensure = true
    )
    {
        IEnumerable<EntityUid> members = [];
        _npcFactionMemberQuery ??= GetEntityQuery<NpcFactionMemberComponent>();

        members = ensure
            ? input.Select(x => (x, EnsureComp<NpcFactionMemberComponent>(x)).x)
            : input.Where(x => _npcFactionMemberQuery.Value.HasComp(x));

        _npcFactionSystem ??= GetSys<NpcFactionSystem>();

        foreach (var member in members)
        {
            _npcFactionSystem.AddFaction(member, factionData.Id);
            yield return member;
        }
    }

    #endregion

    #region Remove

    [CommandImplementation("rmFaction")]
    private EntityUid? RemoveFaction(
        [CommandInvocationContext] IInvocationContext ctx,
        [PipedArgument] EntityUid input,
        [CommandArgument] Prototype<NpcFactionPrototype> factionData
    )
    {
        _npcFactionSystem ??= GetSys<NpcFactionSystem>();
        if (!HasComp<NpcFactionMemberComponent>(input))
        {
            ctx.ReportError(new ComponentNotExists());
            return null;
        }

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
        IEnumerable<EntityUid> members = [];
        _npcFactionMemberQuery ??= GetEntityQuery<NpcFactionMemberComponent>();

        members = input.Where(x => _npcFactionMemberQuery.Value.HasComp(x));

        _npcFactionSystem ??= GetSys<NpcFactionSystem>();

        foreach (var member in members)
        {
            _npcFactionSystem.RemoveFaction(member, factionData.Id);
            yield return member;
        }
    }

    #endregion

    #region Clear

    [CommandImplementation("clearFaction")]
    private EntityUid? ClearFaction(
        [CommandInvocationContext] IInvocationContext ctx,
        [PipedArgument] EntityUid input
    )
    {
        _npcFactionSystem ??= GetSys<NpcFactionSystem>();
        if (!HasComp<NpcFactionMemberComponent>(input))
        {
            ctx.ReportError(new ComponentNotExists());
            return null;
        }

        _npcFactionSystem.ClearFactions(input);
        return input;
    }

    [CommandImplementation("clearFaction")]
    private IEnumerable<EntityUid> ClearFaction(
        [CommandInvocationContext] IInvocationContext ctx,
        [PipedArgument] IEnumerable<EntityUid> input
    )
    {
        IEnumerable<EntityUid> members = [];
        _npcFactionMemberQuery ??= GetEntityQuery<NpcFactionMemberComponent>();

        members = input.Where(x => _npcFactionMemberQuery.Value.HasComp(x));

        _npcFactionSystem ??= GetSys<NpcFactionSystem>();

        foreach (var member in members)
        {
            _npcFactionSystem.ClearFactions(member);
            yield return member;
        }
    }

    #endregion

    #endregion
}

public record struct ComponentNotExists : IConError
{
    public FormattedMessage DescribeInner()
    {
        return FormattedMessage.FromMarkupOrThrow("У сущности нет компонента NpcFactionMember.");
    }

    public string? Expression { get; set; }
    public Vector2i? IssueSpan { get; set; }
    public StackTrace? Trace { get; set; }
}
