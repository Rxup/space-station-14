using Content.Server.Construction;
using Content.Shared._Lavaland.LimitedUsage;
using Content.Shared.Construction.EntitySystems;

namespace Content.Server._Lavaland.LimitedUsage;

public sealed class NoLavalandUsageSystem : SharedNoLavalandUsageSystem
{
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ConstructionChangeEntityEvent>(AfterConstruction);
    }

    private void AfterConstruction(ConstructionChangeEntityEvent ev)
    {
        if(!IsApply(ev.Old) && !IsApply(ev.New))
            return; // если все сущности не подходят под ограничения

        if (QueryLimit.HasComp(ev.Old))
        {
            _transform.Unanchor(ev.New);
            return;
        }

        if (QueryLimit.HasComp(ev.New))
        {
            _transform.Unanchor(ev.New);
            return;
        }
    }
}
