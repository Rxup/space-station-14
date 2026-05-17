using Content.Shared.Backmen.Abilities.Psionics.Events;
using Content.Shared.Backmen.Psionics.Events;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.Interaction.Events;
using Content.Shared.Verbs;

namespace Content.Shared.Backmen.Abilities.Psionics;

public abstract partial class SharedTelegnosisPowerSystem : StatusEffectGrantedPowerSystem<TelegnosisPowerComponent, TelegnosisPowerActionEvent>
{
    [Dependency] private SharedDoAfterSystem _doAfterSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TelegnosticProjectionComponent, InteractionAttemptEvent>(OnInteraction);
        SubscribeLocalEvent<TelegnosticProjectionComponent, BoundUserInterfaceMessageAttempt>(OnUiAttempt);
        SubscribeLocalEvent<TelegnosticProjectionComponent, GetVerbsEvent<Verb>>(OnGetVerbs);
    }

    private void OnGetVerbs(Entity<TelegnosticProjectionComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!(ent.Comp.IsTrapped || TerminatingOrDeleted(ent.Comp.Host)))
            return;

        if(!args.CanInteract || !args.CanComplexInteract)
            return;

        var performer = args.User;
        var tool = args.Using;

        args.Verbs.Add(new Verb()
        {
            Priority = 11,
            Text = Loc.GetString("telegnostic-extract-brain"),
            Act = () =>
            {
                var ev = new TelegnosticGetBrainDoAfterEvent();
                var d = new DoAfterArgs(EntityManager, performer, TimeSpan.FromSeconds(30), ev, ent.Owner, used: tool)
                {
                    BlockDuplicate = true,
                    BreakOnDamage = true,
                    BreakOnDropItem = true,
                    BreakOnMove = true,
                    BreakOnWeightlessMove = true,
                    NeedHand = true,
                };
                _doAfterSystem.TryStartDoAfter(d);
            },
            Impact = LogImpact.High,
        });
    }

    private void OnUiAttempt(Entity<TelegnosticProjectionComponent> ent, ref BoundUserInterfaceMessageAttempt args)
    {
        args.Cancel();
    }

    private void OnInteraction(Entity<TelegnosticProjectionComponent> ent, ref InteractionAttemptEvent args)
    {
        args.Cancelled = true;
    }
}
