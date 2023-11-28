using System.Numerics;
using Content.Server.Movement.Systems;
using Content.Shared.Item.Optic;
using Content.Shared.Movement.Components;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Robust.Server.Player;

namespace Content.Server.Backmen.Item.Optic;

public sealed class OpticZoomEffectSystem : EntitySystem
{
    [Dependency] private readonly IEntityManager _entManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<OpticZoomEffectComponent, OpticZoomEffectActionEvent>(OnToggleZoom);
    }

    private void OnToggleZoom(Entity<OpticZoomEffectComponent> ent, ref OpticZoomEffectActionEvent args)
    {
        if (TryComp<ContentEyeComponent>(ent, out var eyeComp))
        {
            var zoom = new Vector2(ent.Comp.TargetZoom, ent.Comp.TargetZoom);
            if (Math.Abs(eyeComp.TargetZoom.X - ent.Comp.TargetZoom) < 0.1f)
            {
                zoom = new Vector2(1f, 1f);
            }

            _entManager.System<ContentEyeSystem>().SetZoom(args.Performer, zoom, false, eyeComp);
        }
    }
}
