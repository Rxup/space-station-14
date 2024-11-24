using System.Linq;
using Content.Server.Administration;
using Content.Server.Silicons.Laws;
using Content.Shared.Administration;
using Content.Shared.Silicons.Laws;
using Content.Shared.Silicons.Laws.Components;
using Robust.Shared.Toolshed;
using Robust.Shared.Toolshed.TypeParsers;

namespace Content.Server.Backmen.Administration.Commands.Toolshed;

[ToolshedCommand, AdminCommand(AdminFlags.Admin)]
public sealed class LawsCommand : ToolshedCommand
{
    private SiliconLawSystem? _law;

    [CommandImplementation("list")]
    public IEnumerable<EntityUid> List()
    {
        var query = EntityManager.EntityQueryEnumerator<SiliconLawBoundComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            yield return uid;
        }
    }

    [CommandImplementation("get")]
    public IEnumerable<string> Get([PipedArgument] EntityUid lawbound)
    {
        _law ??= GetSys<SiliconLawSystem>();

        foreach (var law in _law.GetLaws(lawbound).Laws)
        {
            yield return $"law {law.LawIdentifierOverride ?? law.Order.ToString()}: {Loc.GetString(law.LawString)}";
        }
    }

    [CommandImplementation("set")]
    public EntityUid? SetLaws(
        [CommandInvocationContext] IInvocationContext ctx,
        [PipedArgument] EntityUid input,
        [CommandArgument] Prototype<SiliconLawsetPrototype> siliconLawSetPrototype
    )
    {
        _law ??= GetSys<SiliconLawSystem>();

        _law.SetLaws(input, siliconLawSetPrototype.Value);
        return input;
    }

    [CommandImplementation("set")]
    public IEnumerable<EntityUid> SetLaws(
        [CommandInvocationContext] IInvocationContext ctx,
        [PipedArgument] IEnumerable<EntityUid> input,
        [CommandArgument] Prototype<SiliconLawsetPrototype> siliconLawSetPrototype
    )
    {
        return input.Where(ent => SetLaws(ctx, ent, siliconLawSetPrototype) != null);
    }

    [CommandImplementation("override")]
    public EntityUid? OverrideLaw(
        [CommandInvocationContext] IInvocationContext ctx,
        [PipedArgument] EntityUid input,
        [CommandArgument] int index,
        [CommandArgument] string lawString
    )
    {
        if (index < 0)
            return null;
        _law ??= GetSys<SiliconLawSystem>();

        var laws = _law.GetLaws(input);
        var law = laws.Laws.FirstOrDefault(x => x.Order == index);
        if (law == null)
        {
            laws.Laws.Insert(index,
                new SiliconLaw()
                {
                    Order = index,
                    LawString = lawString,
                }
            );
        }
        else
        {
            law.LawString = lawString;
        }

        return input;
    }

    [CommandImplementation("override")]
    public IEnumerable<EntityUid> OverrideLaw(
        [CommandInvocationContext] IInvocationContext ctx,
        [PipedArgument] IEnumerable<EntityUid> input,
        [CommandArgument] int index,
        [CommandArgument] string lawString
    )
    {
        return input.Where(uid => OverrideLaw(ctx, uid, index, lawString) != null);
    }
}
