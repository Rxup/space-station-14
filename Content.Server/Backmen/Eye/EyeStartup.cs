using Content.Server.Ghost.Components;
using Content.Server.Backmen.Species.Shadowkin.Systems;
using Content.Shared.Eye;
using Content.Shared.Ghost;
using Robust.Server.GameObjects;

namespace Content.Server.Backmen.Eye;

public sealed class EyeMapInit : EntityEventArgs
{
    public Entity<EyeComponent> Target;
}

/// <summary>
///     Place to handle eye component startup for whatever systems.
/// </summary>
public sealed class EyeStartup : EntitySystem
{
    [Dependency] private readonly ShadowkinDarkSwapSystem _shadowkinPowerSystem = default!;
    [Dependency] private EyeSystem _eye = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EyeComponent, MapInitEvent>(OnEyeStartup);
        SubscribeLocalEvent<EyeMapInit>(OnEyeInit);
    }

    private void OnEyeInit(EyeMapInit ev)
    {
        if (HasComp<GhostComponent>(ev.Target))
            _eye.SetVisibilityMask(ev.Target, ev.Target.Comp.VisibilityMask | (int) VisibilityFlags.AIEye, ev.Target.Comp);

        _shadowkinPowerSystem.SetVisibility(ev.Target, HasComp<GhostComponent>(ev.Target));
    }

    private void OnEyeStartup(EntityUid uid, EyeComponent component, MapInitEvent args)
    {
        RaiseLocalEvent(uid,new EyeMapInit
        {
            Target = (uid,component)
        }, true);
    }
}
