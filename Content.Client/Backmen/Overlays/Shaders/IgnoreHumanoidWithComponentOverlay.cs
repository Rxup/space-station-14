using Content.Shared.Humanoid;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;

namespace Content.Client.Backmen.Overlays.Shaders;

/// <summary>
/// Client-side sprite filter for shadowkin dark swap.
/// Hides humanoid sprites unless they have an allowed component (e.g. also dark-swapped).
/// </summary>
public sealed partial class IgnoreHumanoidWithComponentOverlay : Overlay
{
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private IEntityManager _entityManager = default!;

    public List<Component> IgnoredComponents = new();
    public List<Component> AllowAnywayComponents = new();
    private readonly List<EntityUid> _nonVisibleList = new();

    public IgnoreHumanoidWithComponentOverlay()
    {
        IoCManager.InjectDependencies(this);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var spriteQuery = _entityManager.GetEntityQuery<SpriteComponent>();

        foreach (var humanoid in _entityManager.EntityQuery<HumanoidProfileComponent>(true))
        {
            if (_playerManager.LocalPlayer?.ControlledEntity == humanoid.Owner)
                continue;

            var show = true;
            foreach (var comp in IgnoredComponents)
            {
                if (!_entityManager.HasComponent(humanoid.Owner, comp.GetType()))
                    continue;

                show = false;
                break;
            }

            foreach (var comp in AllowAnywayComponents)
            {
                if (!_entityManager.HasComponent(humanoid.Owner, comp.GetType()))
                    continue;

                show = true;
                break;
            }

            if (show)
            {
                Reset(humanoid.Owner);
                continue;
            }

            if (!spriteQuery.TryGetComponent(humanoid.Owner, out var sprite))
                continue;

            if (!sprite.Visible || _nonVisibleList.Contains(humanoid.Owner))
                continue;

            sprite.Visible = false;
            _nonVisibleList.Add(humanoid.Owner);
        }

        foreach (var uid in _nonVisibleList.ToArray())
        {
            if (!_entityManager.Deleted(uid))
                continue;

            _nonVisibleList.Remove(uid);
        }
    }

    public void Reset()
    {
        foreach (var uid in _nonVisibleList.ToArray())
        {
            Reset(uid);
        }
    }

    public void Reset(EntityUid entity)
    {
        if (!_nonVisibleList.Contains(entity))
            return;

        _nonVisibleList.Remove(entity);

        if (_entityManager.TryGetComponent<SpriteComponent>(entity, out var sprite))
            sprite.Visible = true;
    }
}
