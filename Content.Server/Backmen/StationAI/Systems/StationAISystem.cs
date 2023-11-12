using Content.Server.Backmen.Abilities.Psionics;
using Content.Shared.Actions;
using Content.Shared.Backmen.EntityHealthBar;
using Robust.Shared.Prototypes;
using Content.Shared.Backmen.StationAI.Events;

namespace Content.Shared.Backmen.StationAI;

public sealed class StationAISystem : EntitySystem
{
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StationAIComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<StationAIComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<StationAIComponent, EntityTerminatingEvent>(OnTerminated);

        SubscribeLocalEvent<AIHealthOverlayEvent>(OnHealthOverlayEvent);
    }

    private void OnTerminated(Entity<StationAIComponent> ent, ref EntityTerminatingEvent args)
    {
        if (!ent.Comp.ActiveEye.IsValid())
        {
            return;
        }
        QueueDel(ent.Comp.ActiveEye);
    }

    private void OnStartup(EntityUid uid, StationAIComponent component, ComponentStartup args)
    {
        _actions.AddAction(uid, ref component.ActionId, component.Action);
    }

    private void OnShutdown(EntityUid uid, StationAIComponent component, ComponentShutdown args)
    {
        _actions.RemoveAction(uid, component.ActionId);
    }

    private void OnHealthOverlayEvent(AIHealthOverlayEvent args)
    {
        if (HasComp<ShowHealthBarsComponent>(args.Performer))
        {
            RemCompDeferred<ShowHealthBarsComponent>(args.Performer);
        }
        else
        {
            var comp = EnsureComp<ShowHealthBarsComponent>(args.Performer);
            comp.DamageContainers.Clear();
            comp.DamageContainers.Add("Biological");
            comp.DamageContainers.Add("HalfSpirit");
            Dirty(args.Performer, comp);
        }
        args.Handled = true;
    }
}
