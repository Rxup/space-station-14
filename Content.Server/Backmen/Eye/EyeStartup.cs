using Content.Server.Ghost.Components;
using Content.Server.Backmen.Species.Shadowkin.Systems;
using Content.Shared.Eye;
using Content.Shared.Ghost;
using Robust.Server.GameObjects;

namespace Content.Server.Backmen.Eye;

/// <summary>
///     Place to handle eye component startup for whatever systems.
/// </summary>
public sealed class EyeStartup : EntitySystem
{
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly ShadowkinDarkSwapSystem _shadowkinPowerSystem = default!;
    [Dependency] private EyeSystem _eye = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EyeComponent, ComponentStartup>(OnEyeStartup);
    }

    private void OnEyeStartup(EntityUid uid, EyeComponent component, ComponentStartup args)
    {
        if (HasComp<GhostComponent>(uid))
            _eye.SetVisibilityMask(uid, component.VisibilityMask | (int) VisibilityFlags.AIEye, component);

        _shadowkinPowerSystem.SetVisibility(uid, _entityManager.HasComponent<GhostComponent>(uid));
    }
}
