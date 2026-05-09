using Content.Shared.Backmen.GhostTheme;
using Content.Shared.GameTicking;
using Robust.Client.GameObjects;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;

namespace Content.Client.Backmen.GhostTheme;

public sealed partial class GhostThemeSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private SpriteSystem _spriteSystem = default!;

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

    private readonly EntProtoId MobObserver = "MobObserver";

    public void Apply(EntityUid uid, GhostThemePrototype ghostThemePrototype)
    {
        var rendered = Spawn(MobObserver, ghostThemePrototype.Components);
        _spriteSystem.CopySprite(rendered, uid);
        QueueDel(rendered);
    }
}
