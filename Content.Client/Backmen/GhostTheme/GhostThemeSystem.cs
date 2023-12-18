using Content.Shared.Backmen.GhostTheme;
using Content.Shared.GameTicking;
using Robust.Client.GameObjects;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;

namespace Content.Client.Backmen.GhostTheme;

public sealed class GhostThemeSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GhostThemeComponent, AfterAutoHandleStateEvent>(OnInit);
        SubscribeNetworkEvent<TickerJoinGameEvent>(JoinGame);
    }

    private void JoinGame(TickerJoinGameEvent ev)
    {
        var ghostTheme = _cfg.GetCVar(Shared.Backmen.CCVar.CCVars.SponsorsSelectedGhost);
        if (string.IsNullOrEmpty(ghostTheme))
        {
            return;
        }
        RaiseNetworkEvent(new RequestGhostThemeEvent(ghostTheme));
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
