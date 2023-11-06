using Content.Shared.Backmen.GhostTheme;
using Robust.Client.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager;

namespace Content.Client.Backmen.GhostTheme;

public sealed class GhostThemeSystem: EntitySystem
{
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly ISerializationManager _serialization = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GhostThemeComponent, AfterAutoHandleStateEvent>(OnInit);
    }

    private void OnInit(EntityUid uid, GhostThemeComponent component, ref AfterAutoHandleStateEvent args)
    {
        if (component.GhostTheme == null
            || !_prototypeManager.TryIndex<GhostThemePrototype>(component.GhostTheme, out var ghostThemePrototype))
        {
            return;
        }

        Apply(uid, ghostThemePrototype);
    }

    public void Apply(EntityUid uid, GhostThemePrototype ghostThemePrototype)
    {
        foreach (var entry in ghostThemePrototype.Components.Values)
        {
            if (entry.Component is SpriteComponent spriteComponent && EntityManager.TryGetComponent<SpriteComponent>(uid, out var targetsprite))
            {
                targetsprite.CopyFrom(spriteComponent);
            }
        }
    }
}
