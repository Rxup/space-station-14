using Content.Shared.Examine;
using Content.Shared.Interaction.Events;

namespace Content.Shared.Heretic;

public abstract class SharedGhoulSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GhoulComponent, AttackAttemptEvent>(OnTryAttack);
        SubscribeLocalEvent<GhoulComponent, ExaminedEvent>(OnExamine);
    }

    private void OnTryAttack(Entity<GhoulComponent> ent, ref AttackAttemptEvent args)
    {
        // prevent attacking owner and other heretics
        if (args.Target == ent.Owner || HasComp<HereticComponent>(args.Target))
            args.Cancel();
    }

    private void OnExamine(Entity<GhoulComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("examine-system-cant-see-entity"));
    }
}
