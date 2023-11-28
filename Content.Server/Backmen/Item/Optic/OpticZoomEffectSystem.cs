using System.Numerics;
using Content.Server.Movement.Systems;
using Content.Shared.Item.Optic;
using Content.Shared.Movement.Components;
using Robust.Server.Player;

namespace Content.Server.Backmen.Item.Optic;

public sealed class OpticZoomEffectSystem : EntitySystem
{
    [Dependency] private readonly ContentEyeSystem _contentEyeSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<OpticZoomEffectComponent, OpticZoomEffectActionEvent>(OnToggleZoom);
        SubscribeLocalEvent<OpticZoomEffectComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnShutdown(Entity<OpticZoomEffectComponent> ent, ref ComponentShutdown args)
    {
        if (!TryComp<ContentEyeComponent>(ent, out var eyeComp))
            return;
        _contentEyeSystem.ResetZoom(ent.Owner, eyeComp);
    }

    private void OnToggleZoom(Entity<OpticZoomEffectComponent> ent, ref OpticZoomEffectActionEvent args)
    {
        if (!TryComp<ContentEyeComponent>(ent, out var eyeComp))
            return;
        var zoom = new Vector2(ent.Comp.TargetZoom, ent.Comp.TargetZoom);
        if (Math.Abs(eyeComp.TargetZoom.X - ent.Comp.TargetZoom) < 0.1f)
        {
            zoom = new Vector2(1f, 1f);
        }

        _contentEyeSystem.SetZoom(args.Performer, zoom, false, eyeComp);
    }
}
