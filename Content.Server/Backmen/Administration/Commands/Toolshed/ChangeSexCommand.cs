using System.Collections;
using System.Diagnostics;
using System.Linq;
using Content.Server.Administration;
using Content.Server.Humanoid;
using Content.Shared.Administration;
using Content.Shared.Humanoid;
using Robust.Shared.Enums;
using Robust.Shared.GameObjects.Components.Localization;
using Robust.Shared.Toolshed;
using Robust.Shared.Toolshed.Errors;
using Robust.Shared.Toolshed.Syntax;
using Robust.Shared.Utility;

namespace Content.Server.Backmen.Administration.Commands.Toolshed;

[ToolshedCommand, AdminCommand(AdminFlags.Fun)]
public sealed class ChangeSexCommand : ToolshedCommand
{
    private HumanoidProfileSystem? _appearanceSystem;

    #region base

    private EntityUid? ChangeSex(
        [CommandInvocationContext] IInvocationContext ctx,
        [PipedArgument] EntityUid input,
        Sex sex
    )
    {
        if (!EntityManager.TryGetComponent<HumanoidProfileComponent>(input, out var HumanoidProfileComponent))
        {
            ctx.ReportError(new NotHumanoidError());
            return null;
        }

        _appearanceSystem ??= GetSys<HumanoidProfileSystem>();

        HumanoidProfileComponent.Gender = sex switch
        {
            Sex.Male => Gender.Male,
            Sex.Female => Gender.Female,
            Sex.Unsexed => Gender.Neuter,
            _ => Gender.Epicene
        };

        if (EntityManager.TryGetComponent<GrammarComponent>(input, out var grammarComponent))
        {
            grammarComponent.Gender = HumanoidProfileComponent.Gender;
            EntityManager.Dirty(input, grammarComponent);
        }

        _appearanceSystem.SetSex(input, sex, humanoid: HumanoidProfileComponent);
        EntityManager.Dirty(input, HumanoidProfileComponent);
        return input;
    }

    private IEnumerable<EntityUid> ChangeSex(
        [CommandInvocationContext] IInvocationContext ctx,
        [PipedArgument] IEnumerable<EntityUid> input,
        Sex sex
    )
        => input.Select(x => ChangeSex(ctx, x, sex)).Where(x => x is not null).Select(x => (EntityUid) x!);

    #endregion

    [CommandImplementation("setMale")]
    public EntityUid? SetMale(
        [CommandInvocationContext] IInvocationContext ctx,
        [PipedArgument] EntityUid input)
    {
        return ChangeSex(ctx, input, Sex.Male);
    }

    [CommandImplementation("setMale")]
    public IEnumerable<EntityUid> SetMale(
        [CommandInvocationContext] IInvocationContext ctx,
        [PipedArgument] IEnumerable<EntityUid> input)
    {
        return ChangeSex(ctx, input, Sex.Male);
    }

    [CommandImplementation("setFemale")]
    public EntityUid? SetFemale(
        [CommandInvocationContext] IInvocationContext ctx,
        [PipedArgument] EntityUid input)
    {
        return ChangeSex(ctx, input, Sex.Female);
    }

    [CommandImplementation("setFemale")]
    public IEnumerable<EntityUid> SetFemale(
        [CommandInvocationContext] IInvocationContext ctx,
        [PipedArgument] IEnumerable<EntityUid> input)
    {
        return ChangeSex(ctx, input, Sex.Female);
    }

    [CommandImplementation("setUnsexed")]
    public EntityUid? SetUnsexed(
        [CommandInvocationContext] IInvocationContext ctx,
        [PipedArgument] EntityUid input)
    {
        return ChangeSex(ctx, input, Sex.Unsexed);
    }

    [CommandImplementation("setUnsexed")]
    public IEnumerable<EntityUid> SetUnsexed(
        [CommandInvocationContext] IInvocationContext ctx,
        [PipedArgument] IEnumerable<EntityUid> input)
    {
        return ChangeSex(ctx, input, Sex.Unsexed);
    }
}

public record struct NotHumanoidError : IConError
{
    public FormattedMessage DescribeInner()
    {
        return FormattedMessage.FromMarkup(
            "У сущности нет компонента HumanoidProfileComponent.");
    }

    public string? Expression { get; set; }
    public Vector2i? IssueSpan { get; set; }
    public StackTrace? Trace { get; set; }
}
