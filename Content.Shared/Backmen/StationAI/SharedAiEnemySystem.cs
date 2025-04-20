using Content.Shared.Backmen.StationAI.Components;
using Content.Shared.Examine;
using Content.Shared.Verbs;

namespace Content.Shared.Backmen.StationAI;

public abstract class SharedAiEnemySystem : EntitySystem
{
    protected EntityQuery<BorgAINTComponent> EntityQuery;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AIEnemyNTComponent, ExaminedEvent>(OnExamine);
        SubscribeLocalEvent<CanBeBorgNTEnemyComponent, GetVerbsEvent<AlternativeVerb>>(OnMarkAsTarget);
        EntityQuery = GetEntityQuery<BorgAINTComponent>();
    }

    private void OnExamine(Entity<AIEnemyNTComponent> ent, ref ExaminedEvent args)
    {
        var source = ent.Comp.Source ?? EntityUid.Invalid;
        if(TerminatingOrDeleted(source))
            source = EntityUid.Invalid;
        args.PushMarkup(Loc.GetString("aienemy-examine", ("name", source)));
    }

    protected virtual void ToggleEnemy(EntityUid u, EntityUid target)
    {
        // noop is shared
    }

    private void OnMarkAsTarget(Entity<CanBeBorgNTEnemyComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!EntityQuery.HasComponent(args.User))
            return;

        var u = args.User;
        AlternativeVerb verb = new()
        {
            Act = () =>
            {
                ToggleEnemy(u, ent);
            },
            Text = Loc.GetString("sai-enemy-verb"),
            Priority = 2
        };

        args.Verbs.Add(verb);
    }
}
