using System.Diagnostics;
using System.Linq;
using Content.Server.Administration;
using Content.Shared.Access;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Administration;
using Content.Shared.Roles;
using Robust.Shared.Toolshed;
using Robust.Shared.Toolshed.Errors;
using Robust.Shared.Toolshed.TypeParsers;
using Robust.Shared.Utility;

namespace Content.Server.Backmen.Administration.Commands.Toolshed;

[ToolshedCommand, AdminCommand(AdminFlags.Fun)]
public sealed class ChangeAccessReaderCommand : ToolshedCommand
{
    private AccessReaderSystem? _accessReaderSystem;

    #region Add

    [CommandImplementation("addAccessReader")]
    private EntityUid? AddAccessReader(
        [CommandInvocationContext] IInvocationContext ctx,
        [PipedArgument] EntityUid input,
        [CommandArgument] Prototype<AccessLevelPrototype> accessPrototype
    )
    {
        _accessReaderSystem ??= GetSys<AccessReaderSystem>();
        if (!TryComp(input, out AccessReaderComponent? comp))
        {
            ctx.ReportError(new AccessReaderCompNotExists());
            return null;
        }

        _accessReaderSystem.SetAccess((input, comp), accessPrototype.Id);
        return input;
    }

    [CommandImplementation("addAccessReader")]
    private IEnumerable<EntityUid> AddAccessReader(
        [CommandInvocationContext] IInvocationContext ctx,
        [PipedArgument] IEnumerable<EntityUid> input,
        [CommandArgument] Prototype<AccessLevelPrototype> accessPrototype
    )
    {
        return input.Where(ent => AddAccessReader(ctx, ent, accessPrototype) != null);
    }

    [CommandImplementation("addGroupAccessReader")]
    private EntityUid? AddGroupAccessReader(
        [CommandInvocationContext] IInvocationContext ctx,
        [PipedArgument] EntityUid input,
        [CommandArgument] Prototype<AccessGroupPrototype> accessGroupPrototype
    )
    {
        _accessReaderSystem ??= GetSys<AccessReaderSystem>();
        if (!TryComp(input, out AccessReaderComponent? comp))
        {
            ctx.ReportError(new AccessReaderCompNotExists());
            return null;
        }

        _accessReaderSystem.AddAccessByGroup((input, comp), accessGroupPrototype.Id);
        return input;
    }

    [CommandImplementation("addGroupAccessReader")]
    private IEnumerable<EntityUid> AddGroupAccessReader(
        [CommandInvocationContext] IInvocationContext ctx,
        [PipedArgument] IEnumerable<EntityUid> input,
        [CommandArgument] Prototype<AccessGroupPrototype> accessGroupPrototype
    )
    {
        return input.Where(ent => AddGroupAccessReader(ctx, ent, accessGroupPrototype) != null);
    }

    [CommandImplementation("addJobAccessReader")]
    private EntityUid? AddJobAccessReader(
        [CommandInvocationContext] IInvocationContext ctx,
        [PipedArgument] EntityUid input,
        [CommandArgument] Prototype<JobPrototype> jobPrototype
    )
    {
        _accessReaderSystem ??= GetSys<AccessReaderSystem>();
        if (!TryComp(input, out AccessReaderComponent? comp))
        {
            ctx.ReportError(new AccessReaderCompNotExists());
            return null;
        }

        _accessReaderSystem.AddAccessByJob((input, comp), jobPrototype.Value);

        return input;
    }

    [CommandImplementation("addJobAccessReader")]
    private IEnumerable<EntityUid> AddJobAccessReader(
        [CommandInvocationContext] IInvocationContext ctx,
        [PipedArgument] IEnumerable<EntityUid> input,
        [CommandArgument] Prototype<JobPrototype> jobPrototype
    )
    {
        return input.Where(ent => AddJobAccessReader(ctx, ent, jobPrototype) != null);
    }

    #endregion

    #region Remove

    [CommandImplementation("rmAccessReader")]
    private EntityUid? RemoveAccessReader(
        [CommandInvocationContext] IInvocationContext ctx,
        [PipedArgument] EntityUid input,
        [CommandArgument] Prototype<AccessLevelPrototype> accessPrototype
    )
    {
        _accessReaderSystem ??= GetSys<AccessReaderSystem>();
        if (!TryComp(input, out AccessReaderComponent? comp))
        {
            ctx.ReportError(new AccessReaderCompNotExists());
            return null;
        }

        _accessReaderSystem.RemoveAccess((input, comp), accessPrototype.Id);

        return input;
    }

    [CommandImplementation("rmAccessReader")]
    private IEnumerable<EntityUid> RemoveAccessReader(
        [CommandInvocationContext] IInvocationContext ctx,
        [PipedArgument] IEnumerable<EntityUid> input,
        [CommandArgument] Prototype<AccessLevelPrototype> accessPrototype
    )
    {
        return input.Where(ent => RemoveAccessReader(ctx, ent, accessPrototype) != null);
    }

    [CommandImplementation("rmGroupAccessReader")]
    private EntityUid? RemoveGroupAccessReader(
        [CommandInvocationContext] IInvocationContext ctx,
        [PipedArgument] EntityUid input,
        [CommandArgument] Prototype<AccessGroupPrototype> accessGroupPrototype
    )
    {
        _accessReaderSystem ??= GetSys<AccessReaderSystem>();

        if (!TryComp(input, out AccessReaderComponent? comp))
        {
            ctx.ReportError(new AccessReaderCompNotExists());
            return null;
        }

        _accessReaderSystem.RemoveAccessByGroup((input, comp), accessGroupPrototype.Id);

        return input;
    }

    [CommandImplementation("rmGroupAccessReader")]
    private IEnumerable<EntityUid> RemoveGroupAccessReader(
        [CommandInvocationContext] IInvocationContext ctx,
        [PipedArgument] IEnumerable<EntityUid> input,
        [CommandArgument] Prototype<AccessGroupPrototype> accessGroupPrototype
    )
    {
        return input.Where(ent => RemoveGroupAccessReader(ctx, ent, accessGroupPrototype) != null);
    }

    [CommandImplementation("rmJobAccessReader")]
    private EntityUid? RemoveJobAccessReader(
        [CommandInvocationContext] IInvocationContext ctx,
        [PipedArgument] EntityUid input,
        [CommandArgument] Prototype<JobPrototype> jobPrototype
    )
    {
        _accessReaderSystem ??= GetSys<AccessReaderSystem>();

        if (!TryComp(input, out AccessReaderComponent? comp))
        {
            ctx.ReportError(new AccessReaderCompNotExists());
            return null;
        }

        _accessReaderSystem.RemoveAccessByJob((input, comp), jobPrototype.Value);

        return input;
    }

    [CommandImplementation("rmJobAccessReader")]
    private IEnumerable<EntityUid> RemoveJobAccessReader(
        [CommandInvocationContext] IInvocationContext ctx,
        [PipedArgument] IEnumerable<EntityUid> input,
        [CommandArgument] Prototype<JobPrototype> jobPrototype
    )
    {
        return input.Where(ent => RemoveJobAccessReader(ctx, ent, jobPrototype) != null);
    }

    #endregion

    #region Clear

    [CommandImplementation("clearAccessReader")]
    private EntityUid? ClearAccessReader(
        [CommandInvocationContext] IInvocationContext ctx,
        [PipedArgument] EntityUid input
    )
    {
        _accessReaderSystem ??= GetSys<AccessReaderSystem>();

        if (!TryComp(input, out AccessReaderComponent? comp))
        {
            ctx.ReportError(new AccessReaderCompNotExists());
            return null;
        }

        _accessReaderSystem.ClearAccesses((input, comp));

        return input;
    }

    [CommandImplementation("clearAccessReader")]
    private IEnumerable<EntityUid> ClearAccessReader(
        [CommandInvocationContext] IInvocationContext ctx,
        [PipedArgument] IEnumerable<EntityUid> input
    )
    {
        return input.Where(ent => ClearAccessReader(ctx, ent) != null);
    }

    #endregion

    #region Set

    [CommandImplementation("setGroupAccessReader")]
    private EntityUid? SetGroupAccessReader(
        [CommandInvocationContext] IInvocationContext ctx,
        [PipedArgument] EntityUid input,
        [CommandArgument] Prototype<AccessGroupPrototype> accessGroupPrototype
    )
    {
        _accessReaderSystem ??= GetSys<AccessReaderSystem>();

        if (!TryComp(input, out AccessReaderComponent? comp))
        {
            ctx.ReportError(new AccessReaderCompNotExists());
            return null;
        }

        _accessReaderSystem.SetAccessByGroup((input, comp), accessGroupPrototype.Id);

        return input;
    }

    [CommandImplementation("setGroupAccessReader")]
    private IEnumerable<EntityUid> SetGroupAccessReader(
        [CommandInvocationContext] IInvocationContext ctx,
        [PipedArgument] IEnumerable<EntityUid> input,
        [CommandArgument] Prototype<AccessGroupPrototype> accessGroupPrototype
    )
    {
        return input.Where(ent => SetGroupAccessReader(ctx, ent, accessGroupPrototype) != null);
    }

    [CommandImplementation("setJobAccessReader")]
    private EntityUid? SetJobAccessReader(
        [CommandInvocationContext] IInvocationContext ctx,
        [PipedArgument] EntityUid input,
        [CommandArgument] Prototype<JobPrototype> jobPrototype
    )
    {
        _accessReaderSystem ??= GetSys<AccessReaderSystem>();
        if (!TryComp(input, out AccessReaderComponent? comp))
        {
            ctx.ReportError(new AccessReaderCompNotExists());
            return null;
        }

        _accessReaderSystem.SetAccessByJob((input, comp), jobPrototype.Value);
        return input;
    }

    [CommandImplementation("setJobAccessReader")]
    private IEnumerable<EntityUid> SetJobAccessReader(
        [CommandInvocationContext] IInvocationContext ctx,
        [PipedArgument] IEnumerable<EntityUid> input,
        [CommandArgument] Prototype<JobPrototype> jobPrototype
    )
    {
        return input.Where(ent => SetJobAccessReader(ctx, ent, jobPrototype) != null);
    }

    #endregion
}

public record struct AccessReaderCompNotExists : IConError
{
    public FormattedMessage DescribeInner()
    {
        return FormattedMessage.FromMarkupOrThrow("У сущности нет компонента AccessReader.");
    }

    public string? Expression { get; set; }
    public Vector2i? IssueSpan { get; set; }
    public StackTrace? Trace { get; set; }
}
