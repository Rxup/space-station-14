using System.Diagnostics;
using System.Linq;
using Content.Server.Access.Systems;
using Content.Server.Administration;
using Content.Shared.Access;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Administration;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Prototypes;
using Content.Shared.NPC.Systems;
using Content.Shared.Roles;
using Robust.Shared.Toolshed;
using Robust.Shared.Toolshed.Errors;
using Robust.Shared.Toolshed.TypeParsers;
using Robust.Shared.Utility;

namespace Content.Server.Backmen.Administration.Commands.Toolshed;

[ToolshedCommand, AdminCommand(AdminFlags.Fun)]
public sealed class ChangeAccessCommand : ToolshedCommand
{
    private AccessSystem? _accessSystem;

    #region Set

    [CommandImplementation("setGroupAccessLevel")]
    private EntityUid? SetGroupAccessLevel(
        [CommandInvocationContext] IInvocationContext ctx,
        [PipedArgument] EntityUid input,
        [CommandArgument] Prototype<AccessGroupPrototype> accessGroupPrototype
    )
    {
        _accessSystem ??= GetSys<AccessSystem>();

        if (!_accessSystem.TrySetGroup(input, accessGroupPrototype.Id))
        {
            ctx.ReportError(new AccessCompNotExists());
            return null;
        }

        return input;
    }

    [CommandImplementation("setGroupAccessLevel")]
    private IEnumerable<EntityUid> SetGroupAccessLevel(
        [CommandInvocationContext] IInvocationContext ctx,
        [PipedArgument] IEnumerable<EntityUid> input,
        [CommandArgument] Prototype<AccessGroupPrototype> accessGroupPrototype
    )
    {
        return input.Where(ent => SetGroupAccessLevel(ctx, ent, accessGroupPrototype) != null);
    }

    [CommandImplementation("setJobAccessLevel")]
    private EntityUid? SetJobAccessLevel(
        [CommandInvocationContext] IInvocationContext ctx,
        [PipedArgument] EntityUid input,
        [CommandArgument] Prototype<JobPrototype> jobPrototype
    )
    {
        _accessSystem ??= GetSys<AccessSystem>();
        _accessSystem.SetAccessToJob(input, jobPrototype.Value, false);
        return input;
    }

    [CommandImplementation("setJobAccessLevel")]
    private IEnumerable<EntityUid> SetJobAccessLevel(
        [CommandInvocationContext] IInvocationContext ctx,
        [PipedArgument] IEnumerable<EntityUid> input,
        [CommandArgument] Prototype<JobPrototype> jobPrototype
    )
    {
        return input.Where(ent => SetJobAccessLevel(ctx, ent, jobPrototype) != null);
    }

    #endregion

    #region Add

    [CommandImplementation("addAccessLevel")]
    private EntityUid? AddAccessLevel(
        [CommandInvocationContext] IInvocationContext ctx,
        [PipedArgument] EntityUid input,
        [CommandArgument] Prototype<AccessLevelPrototype> accessPrototype
    )
    {
        _accessSystem ??= GetSys<AccessSystem>();

        if (!_accessSystem.TryAddSingleTag(input, accessPrototype.Id))
        {
            ctx.ReportError(new AccessCompNotExists());
            return null;
        }

        return input;
    }

    [CommandImplementation("addAccessLevel")]
    private IEnumerable<EntityUid> AddAccessLevel(
        [CommandInvocationContext] IInvocationContext ctx,
        [PipedArgument] IEnumerable<EntityUid> input,
        [CommandArgument] Prototype<AccessLevelPrototype> accessPrototype
    )
    {
        return input.Where(ent => AddAccessLevel(ctx, ent, accessPrototype) != null);
    }

    [CommandImplementation("addGroupAccessLevel")]
    private EntityUid? AddGroupAccessLevel(
        [CommandInvocationContext] IInvocationContext ctx,
        [PipedArgument] EntityUid input,
        [CommandArgument] Prototype<AccessGroupPrototype> accessGroupPrototype
    )
    {
        _accessSystem ??= GetSys<AccessSystem>();

        if (!_accessSystem.TryAddGroup(input, accessGroupPrototype.Id))
        {
            ctx.ReportError(new AccessCompNotExists());
            return null;
        }

        return input;
    }

    [CommandImplementation("addGroupAccessLevel")]
    private IEnumerable<EntityUid> AddGroupAccessLevel(
        [CommandInvocationContext] IInvocationContext ctx,
        [PipedArgument] IEnumerable<EntityUid> input,
        [CommandArgument] Prototype<AccessGroupPrototype> accessGroupPrototype
    )
    {
        return input.Where(ent => AddGroupAccessLevel(ctx, ent, accessGroupPrototype) != null);
    }

    [CommandImplementation("addJobAccessLevel")]
    private EntityUid? AddJobAccessLevel(
        [CommandInvocationContext] IInvocationContext ctx,
        [PipedArgument] EntityUid input,
        [CommandArgument] Prototype<JobPrototype> jobPrototype
    )
    {
        _accessSystem ??= GetSys<AccessSystem>();

        if (!_accessSystem.TryUnionWithJob(input, jobPrototype.Value, false))
        {
            ctx.ReportError(new AccessCompNotExists());
            return null;
        }

        return input;
    }

    [CommandImplementation("addJobAccessLevel")]
    private IEnumerable<EntityUid> AddJobAccessLevel(
        [CommandInvocationContext] IInvocationContext ctx,
        [PipedArgument] IEnumerable<EntityUid> input,
        [CommandArgument] Prototype<JobPrototype> jobPrototype
    )
    {
        return input.Where(ent => AddJobAccessLevel(ctx, ent, jobPrototype) != null);
    }

    #endregion

    #region Remove

    [CommandImplementation("rmAccessLevel")]
    private EntityUid? RemoveAccessLevel(
        [CommandInvocationContext] IInvocationContext ctx,
        [PipedArgument] EntityUid input,
        [CommandArgument] Prototype<AccessLevelPrototype> accessPrototype
    )
    {
        _accessSystem ??= GetSys<AccessSystem>();

        if (!_accessSystem.TryRemoveSingleTag(input, accessPrototype.Id))
        {
            ctx.ReportError(new AccessCompNotExists());
            return null;
        }

        return input;
    }

    [CommandImplementation("rmAccessLevel")]
    private IEnumerable<EntityUid> RemoveAccessLevel(
        [CommandInvocationContext] IInvocationContext ctx,
        [PipedArgument] IEnumerable<EntityUid> input,
        [CommandArgument] Prototype<AccessLevelPrototype> accessPrototype
    )
    {
        return input.Where(ent => RemoveAccessLevel(ctx, ent, accessPrototype) != null);
    }

    [CommandImplementation("rmGroupAccessLevel")]
    private EntityUid? RemoveGroupAccessLevel(
        [CommandInvocationContext] IInvocationContext ctx,
        [PipedArgument] EntityUid input,
        [CommandArgument] Prototype<AccessGroupPrototype> accessGroupPrototype
    )
    {
        _accessSystem ??= GetSys<AccessSystem>();

        if (!_accessSystem.TryRemoveGroup(input, accessGroupPrototype.Id))
        {
            ctx.ReportError(new AccessCompNotExists());
            return null;
        }

        return input;
    }

    [CommandImplementation("rmGroupAccessLevel")]
    private IEnumerable<EntityUid> RemoveGroupAccessLevel(
        [CommandInvocationContext] IInvocationContext ctx,
        [PipedArgument] IEnumerable<EntityUid> input,
        [CommandArgument] Prototype<AccessGroupPrototype> accessGroupPrototype
    )
    {
        return input.Where(ent => RemoveGroupAccessLevel(ctx, ent, accessGroupPrototype) != null);
    }

    [CommandImplementation("rmJobAccessLevel")]
    private EntityUid? RemoveJobAccessLevel(
        [CommandInvocationContext] IInvocationContext ctx,
        [PipedArgument] EntityUid input,
        [CommandArgument] Prototype<JobPrototype> jobPrototype
    )
    {
        _accessSystem ??= GetSys<AccessSystem>();

        if (!_accessSystem.TryExceptWithJob(input, jobPrototype.Value, false))
        {
            ctx.ReportError(new AccessCompNotExists());
            return null;
        }

        return input;
    }

    [CommandImplementation("rmJobAccessLevel")]
    private IEnumerable<EntityUid> RemoveJobAccessLevel(
        [CommandInvocationContext] IInvocationContext ctx,
        [PipedArgument] IEnumerable<EntityUid> input,
        [CommandArgument] Prototype<JobPrototype> jobPrototype
    )
    {
        return input.Where(ent => RemoveJobAccessLevel(ctx, ent, jobPrototype) != null);
    }

    #endregion

    #region Clear

    [CommandImplementation("clearAccessLevel")]
    private EntityUid? ClearAccessLevel(
        [CommandInvocationContext] IInvocationContext ctx,
        [PipedArgument] EntityUid input
    )
    {
        _accessSystem ??= GetSys<AccessSystem>();

        if (!_accessSystem.TryClearTags(input))
        {
            ctx.ReportError(new AccessCompNotExists());
            return null;
        }

        return input;
    }

    [CommandImplementation("clearAccessLevel")]
    private IEnumerable<EntityUid> ClearAccessLevel(
        [CommandInvocationContext] IInvocationContext ctx,
        [PipedArgument] IEnumerable<EntityUid> input
    )
    {
        return input.Where(ent => ClearAccessLevel(ctx, ent) != null);
    }

    #endregion
}

public record struct AccessCompNotExists : IConError
{
    public FormattedMessage DescribeInner()
    {
        return FormattedMessage.FromMarkupOrThrow("У сущности нет компонента AccessComponent.");
    }

    public string? Expression { get; set; }
    public Vector2i? IssueSpan { get; set; }
    public StackTrace? Trace { get; set; }
}
