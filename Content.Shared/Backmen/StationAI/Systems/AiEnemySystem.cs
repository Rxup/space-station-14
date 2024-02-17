using Content.Shared.Backmen.StationAI.Components;
using Content.Shared.Examine;

namespace Content.Shared.Backmen.StationAI.Systems;

public abstract class SharedAiEnemySystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AIEnemyNTComponent, ExaminedEvent>(OnExamine);
    }

    private void OnExamine(Entity<AIEnemyNTComponent> ent, ref ExaminedEvent args)
    {
        var source = ent.Comp.Source ?? EntityUid.Invalid;
        if(TerminatingOrDeleted(source))
            source = EntityUid.Invalid;
        args.PushMarkup(Loc.GetString("aienemy-examine", ("name", source)));
    }
}
