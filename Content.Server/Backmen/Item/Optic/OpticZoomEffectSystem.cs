using System.Numerics;
using Content.Server.Actions;
using Content.Server.Movement.Systems;
using Content.Shared.Item.Optic;
using Content.Shared.Movement.Components;
using Robust.Shared.Prototypes;

namespace Content.Server.Backmen.Item.Optic;

public sealed class OpticZoomEffectSystem : EntitySystem
{
    [Dependency] private readonly ContentEyeSystem _contentEyeSystem = default!;
    [Dependency] private readonly ActionsSystem _actionsSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<OpticZoomEffectComponent, OpticZoomEffectActionEvent>(OnToggleZoom);
        SubscribeLocalEvent<OpticZoomEffectComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<OpticZoomEffectComponent, ComponentStartup>(OnStartup);
    }

    [ValidatePrototypeId<EntityPrototype>] private const string ActionOpticZoom = "ActionOpticZoom";

    private void OnStartup(Entity<OpticZoomEffectComponent> ent, ref ComponentStartup args)
    {
        _actionsSystem.AddAction(ent.Owner, ref ent.Comp.ActionId, ActionOpticZoom);
    }

    private void OnShutdown(Entity<OpticZoomEffectComponent> ent, ref ComponentShutdown args)
    {
        _actionsSystem.RemoveAction(ent.Owner, ent.Comp.ActionId);

        if (!TryComp<ContentEyeComponent>(ent, out var eyeComp))
            return;

        _contentEyeSystem.ResetZoom(ent.Owner, eyeComp);
    }

    private void OnToggleZoom(Entity<OpticZoomEffectComponent> ent, ref OpticZoomEffectActionEvent args)
    {
        if (!TryComp<ContentEyeComponent>(ent, out var eyeComp))
            return;

        if (ent.Comp.Zoomed)
        {
            _contentEyeSystem.ResetZoom(ent.Owner, eyeComp);
            _actionsSystem.SetCooldown(ent.Comp.ActionId,TimeSpan.FromSeconds(1));
        }
        else
        {
            _contentEyeSystem.SetZoom(args.Performer, new Vector2(ent.Comp.TargetZoom, ent.Comp.TargetZoom), true, eyeComp);
        }

        ent.Comp.Zoomed = !ent.Comp.Zoomed;
    }
}
